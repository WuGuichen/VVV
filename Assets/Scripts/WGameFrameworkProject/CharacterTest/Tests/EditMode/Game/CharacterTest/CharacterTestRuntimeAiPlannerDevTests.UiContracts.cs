using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using MxFramework.Animation;
using MxFramework.Audio;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Combat.Core;
using MxFramework.Combat.GameplayBridge;
using MxFramework.Combat.Physics;
using MxFramework.Combat.Projectiles;
using MxFramework.Combat.Shields;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Gameplay;
using MxFramework.Network;
using MxFramework.Resources;
using MxFramework.Runtime;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using MxFramework.Vfx;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed partial class CharacterTestRuntimeAiPlannerDevTests
    {

        [Test]
        public void RuntimeAiLabPreviewBuilder_VisibleDirectTargetCreatesGoodPreview()
        {
            CharacterTestRuntimeAiPlannerAgentSnapshot agent = CreatePreviewAgent();

            CharacterTestRuntimeAiPlannerDevPreviewItem preview =
                CharacterTestRuntimeAiPlannerDevPreviewBuilder.Build(frame: 17, agent);

            Assert.IsTrue(preview.Available);
            Assert.AreEqual(CharacterTestRuntimeAiPlannerDevPreviewSeverity.Good, preview.Severity);
            Assert.IsTrue(preview.HasGeometry);
            Assert.IsTrue(preview.HasTargetLine);
            Assert.IsTrue(preview.HasDistanceWitness);
            Assert.AreEqual(new Vector3(1.4f, 0.8f, 2.1f), preview.DistanceSourcePoint);
            Assert.AreEqual(new Vector3(2.6f, 0.8f, 2.4f), preview.DistanceTargetPoint);
            Assert.AreEqual(2.06f, preview.TargetRootDistance, 0.001f);
            Assert.AreEqual(16L, preview.TargetDistanceGeometryFrame);
            Assert.AreEqual("hurt.head", preview.TargetLogicalHurtboxId);
            StringAssert.StartsWith("Hurtbox gap ", preview.FormattedDistance);
            Assert.IsTrue(preview.HasAttackRange);
            Assert.IsTrue(preview.HasMoveArrow);
            Assert.AreEqual(17, preview.Frame);
            Assert.AreEqual(3, preview.AgentEntityId);
            Assert.AreEqual(9, preview.TargetEntityId);
            StringAssert.Contains("视线畅通", preview.StatusLabel);
            StringAssert.Contains("范围内", preview.StatusLabel);
        }

        [Test]
        public void RuntimeAiLabPreviewBuilder_BlockedTargetIsWarning()
        {
            CharacterTestRuntimeAiPlannerAgentSnapshot agent = CreatePreviewAgent(
                targetVisible: true,
                targetLineOfSightBlocked: true,
                moveDirectPathClear: false);

            CharacterTestRuntimeAiPlannerDevPreviewItem preview =
                CharacterTestRuntimeAiPlannerDevPreviewBuilder.Build(frame: 18, agent);

            Assert.AreEqual(CharacterTestRuntimeAiPlannerDevPreviewSeverity.Warning, preview.Severity);
            StringAssert.Contains("视线受阻", preview.StatusLabel);
        }

        [Test]
        public void RuntimeAiLabPreviewBuilder_PathUnavailableOrHazardIsDanger()
        {
            CharacterTestRuntimeAiPlannerAgentSnapshot pathUnavailable = CreatePreviewAgent(
                targetInAttackRange: false,
                movePathAvailable: false,
                environmentNearHazard: false);
            CharacterTestRuntimeAiPlannerAgentSnapshot hazard = CreatePreviewAgent(
                environmentNearHazard: true);

            CharacterTestRuntimeAiPlannerDevPreviewItem noPathPreview =
                CharacterTestRuntimeAiPlannerDevPreviewBuilder.Build(frame: 19, pathUnavailable);
            CharacterTestRuntimeAiPlannerDevPreviewItem hazardPreview =
                CharacterTestRuntimeAiPlannerDevPreviewBuilder.Build(frame: 20, hazard);

            Assert.AreEqual(CharacterTestRuntimeAiPlannerDevPreviewSeverity.Danger, noPathPreview.Severity);
            Assert.AreEqual(CharacterTestRuntimeAiPlannerDevPreviewSeverity.Danger, hazardPreview.Severity);
            StringAssert.Contains("无路径", noPathPreview.StatusLabel);
        }

        [Test]
        public void RuntimeAiLabPreviewBuilder_UsesShortOverlayLabelsAndSeparateRangeColor()
        {
            CharacterTestRuntimeAiPlannerAgentSnapshot outOfRange = CreatePreviewAgent(
                targetInAttackRange: false,
                selectedActionName: "Movement.ApproachTarget");

            CharacterTestRuntimeAiPlannerDevPreviewItem preview =
                CharacterTestRuntimeAiPlannerDevPreviewBuilder.Build(frame: 22, outOfRange);

            StringAssert.DoesNotContain("Movement.ApproachTarget", preview.Headline);
            StringAssert.Contains("接近", preview.Headline);
            StringAssert.Contains("范围外", preview.StatusLabel);
            Assert.AreEqual(CharacterTestRuntimeAiPlannerDevPreviewColors.Good, preview.TargetLineColor);
            Assert.AreEqual(CharacterTestRuntimeAiPlannerDevPreviewColors.Warning, preview.AttackRangeColor);
        }

        [Test]
        public void RuntimeAiLabPreviewBuilder_MissingSnapshotOrGeometryIsSafe()
        {
            CharacterTestRuntimeAiPlannerDevPreviewItem disabled =
                CharacterTestRuntimeAiPlannerDevPreviewBuilder.Build(
                    frame: null,
                    selectedAgentIndex: 0,
                    overlayEnabled: false);
            CharacterTestRuntimeAiPlannerAgentSnapshot noGeometry = CreatePreviewAgent(
                hasScenePreviewGeometry: false);

            CharacterTestRuntimeAiPlannerDevPreviewItem geometryPreview =
                CharacterTestRuntimeAiPlannerDevPreviewBuilder.Build(frame: 21, noGeometry);

            Assert.IsFalse(disabled.Available);
            Assert.IsTrue(geometryPreview.Available);
            Assert.IsFalse(geometryPreview.HasGeometry);
            Assert.AreEqual(CharacterTestRuntimeAiPlannerDevPreviewSeverity.Neutral, geometryPreview.Severity);
            StringAssert.Contains("No scene preview geometry", geometryPreview.StatusLabel);
        }

        [Test]
        public void RuntimeAiLabPreviewBuilder_MissingWitnessKeepsSingleFrozenActorLine()
        {
            CharacterTestRuntimeAiPlannerAgentSnapshot agent = CreatePreviewAgent(
                hasDistanceWitness: false);

            CharacterTestRuntimeAiPlannerDevPreviewItem preview =
                CharacterTestRuntimeAiPlannerDevPreviewBuilder.Build(frame: 22, agent);

            Assert.IsTrue(preview.HasTargetLine);
            Assert.IsFalse(preview.HasDistanceWitness);
            Assert.AreEqual(preview.SourceCenter, preview.DistanceSourcePoint);
            Assert.AreEqual(preview.TargetCenter, preview.DistanceTargetPoint);
        }

        [Test]
        public void RuntimeAiLabPanelManifest_DeclaresUniqueCommands()
        {
            MxUiViewContract contract =
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelManifest.CreateViewContract();
            string manifestText = File.ReadAllText(
                "Assets/Scripts/WGameFrameworkProject/CharacterTest/UI/FairyGui/"
                + "CharacterTestFairyGuiRuntimeAiPlannerDevPanelManifest.cs");

            Assert.AreEqual("MxCharacterTestRuntimeAiLabUi", CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId);
            Assert.AreEqual(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageBytesKey.PackageId);
            Assert.AreEqual(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Atlas0Key.PackageId);
            StringAssert.Contains(
                @"PackageSourcePath = @""FGUIProject/assets/MxCharacterTestRuntimeAiLabUi/package.xml""",
                manifestText);
            StringAssert.Contains(
                @"PackageBytesPath = @""Assets/Bundles/FGUI/MxCharacterTestRuntimeAiLabUi/MxCharacterTestRuntimeAiLabUi_fui.bytes""",
                manifestText);
            StringAssert.Contains("RecoveryPackageId", manifestText);
            StringAssert.DoesNotContain("LauncherPackageId", manifestText);
            object manifest = typeof(CharacterTestFairyGuiRuntimeAiPlannerDevPanelManifest)
                .GetMethod("Create", BindingFlags.Static | BindingFlags.Public)
                ?.Invoke(null, null);
            Assert.IsNotNull(manifest);
            IList packages = manifest.GetType().GetProperty("Packages")?.GetValue(manifest) as IList;
            IList views = manifest.GetType().GetProperty("Views")?.GetValue(manifest) as IList;
            Assert.IsNotNull(packages);
            Assert.IsNotNull(views);
            Assert.AreEqual(2, packages.Count);
            Assert.AreEqual(2, views.Count);
            object recoveryView = views[1];
            Assert.AreEqual(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.RecoveryPackageId,
                recoveryView.GetType().GetProperty("PackageId")?.GetValue(recoveryView));
            Assert.AreEqual(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.RecoveryComponentName,
                recoveryView.GetType().GetProperty("ComponentName")?.GetValue(recoveryView));
            StringAssert.DoesNotContain("MxCharacterTestCombatHud", manifestText);

            MxUiContractValidationResult validation = contract.Validate();
            Assert.IsTrue(
                validation.Success,
                validation.Errors.Count > 0 ? validation.Errors[0] : string.Empty);
            Assert.AreEqual(
                contract.Commands.Count,
                contract.Commands.Select(command => command.CommandId).Distinct().Count());
            Assert.IsTrue(contract.Commands.Any(
                command => command.CommandId == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.Start));
            Assert.IsTrue(contract.Commands.Any(
                command => command.CommandId == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.DiagnosticsFactsTab));
            Assert.IsTrue(contract.Commands.Any(
                command => command.CommandId == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.Step));
            Assert.IsTrue(contract.Commands.Any(
                command => command.CommandId == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SelectAuthoringWorkspaceRole));
            Assert.IsTrue(contract.Commands.Any(
                command => command.CommandId == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TestCurrentChanges));
            Assert.IsTrue(contract.Commands.Any(
                command => command.CommandId == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.PublishCurrentChanges));
            Assert.IsFalse(contract.Commands.Any(
                command => command.CommandId == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.StepBack));
            Assert.IsFalse(contract.Commands.Any(
                command => command.CommandId == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.StepBack10));
            Assert.IsFalse(contract.Commands.Any(
                command => command.CommandId == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.StepForward10));
            Assert.IsFalse(contract.Commands.Any(
                command => command.CommandId == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.PreviousFrame));
            Assert.IsFalse(contract.Commands.Any(
                command => command.CommandId == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.NextFrame));
            Assert.IsTrue(contract.Commands.Any(
                command => command.CommandId == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.JumpFrame));
            Assert.IsTrue(contract.Commands.Any(
                command => command.CommandId == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.PlaybackSpeed));
            Assert.IsTrue(contract.Commands.Any(
                command => command.CommandId == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.LoopStart));
            Assert.IsTrue(contract.Commands.Any(
                command => command.CommandId == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.LoopEnd));
            Assert.IsTrue(contract.Commands.Any(
                command => command.CommandId == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ToggleLoop));
            Assert.IsTrue(contract.Commands.Any(
                command => command.CommandId == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ToggleEditGate));
            Assert.IsTrue(contract.Commands.Any(
                command => command.CommandId == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TogglePreviewOverlay));
            Assert.IsTrue(contract.Commands.Any(
                command => command.CommandId == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ToggleDetails));
            Assert.IsTrue(contract.Commands.Any(
                command => command.CommandId == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ColdBootstrapVerify));
            Assert.IsTrue(contract.Commands.Any(
                command => command.CommandId == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ToggleSetupSnap));
            Assert.IsTrue(contract.Commands.Any(
                command => command.CommandId == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.MirrorSpawns));
            Assert.IsTrue(contract.Commands.Any(
                command => command.CommandId == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ResetSpawnDefaults));
            Assert.IsTrue(contract.Commands.Any(
                command => command.CommandId == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.FitSetupView));
            Assert.IsTrue(contract.Commands.Any(
                command => command.CommandId == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ToggleSelectedSetupSlot));
            Assert.IsTrue(contract.Commands.Any(
                command => command.CommandId == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.NextSetupBehaviorGroup));
            Assert.IsTrue(contract.Commands.Any(
                command => command.CommandId == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SelectSetupSlot(
                    CharacterTestArenaMatchConfig.Team2Id,
                    2)));
            Assert.IsTrue(contract.Commands.Any(
                command => command.CommandId == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.OpenBehaviorGroup));
            Assert.IsTrue(contract.Commands.Any(
                command => command.CommandId == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReloadProjectDraft));
            Assert.IsTrue(contract.Commands.Any(
                command => command.CommandId == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReapplyProjectDraft));
            Assert.IsTrue(contract.Commands.Any(
                command => command.CommandId == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SetProjectCardDisplayName));
            Assert.IsTrue(contract.Commands.Any(
                command => command.CommandId == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SetProjectCardCost));
            Assert.IsTrue(contract.Commands.Any(
                command => command.CommandId == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SetProjectCardTags));
            Assert.IsTrue(contract.Commands.Any(
                command => command.CommandId == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.DuplicateEntry));
            Assert.IsFalse(contract.Commands.Any(
                command => command.CommandId == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SelectWorkbenchEntry(0)));
            Assert.IsTrue(contract.Commands.Any(
                command => command.CommandId == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.PublishDraft));
            Assert.IsTrue(contract.Commands.Any(
                command => command.CommandId
                    == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SaveBehaviorGroup));
            Assert.IsTrue(contract.Commands.Any(
                command => command.CommandId == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayFreeze));
            Assert.IsTrue(contract.Commands.Any(
                command => command.CommandId == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayValidate));
            Assert.IsTrue(contract.Commands.Any(
                command => command.CommandId == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayCancel));
            Assert.IsTrue(contract.Commands.Any(
                command => command.CommandId == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayRetry));
            Assert.IsTrue(contract.Commands.Any(
                command => command.CommandId
                    == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds
                        .TimelineModeRuntimeTrace));
            Assert.IsTrue(contract.Commands.Any(
                command => command.CommandId
                    == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds
                        .TimelineModeBehaviorPlan));
            Assert.IsTrue(contract.Commands.Any(
                command => command.CommandId
                    == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds
                        .TimelineModeBehaviorLogic));
            StringAssert.Contains(
                "Component(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.BehaviorCardLibrary)",
                manifestText);
            StringAssert.Contains(
                "Component(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.BehaviorCardTimeline)",
                manifestText);
            StringAssert.Contains(
                "List(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkbenchEntryList, \"WorkbenchRows\")",
                manifestText);
            StringAssert.Contains(
                "Graph(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineModeHost)",
                manifestText);
            StringAssert.Contains(
                "Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineModeRuntimeTrace)",
                manifestText);
            StringAssert.Contains(
                "Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineModeBehaviorPlan)",
                manifestText);
            StringAssert.Contains(
                "Button(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineModeBehaviorLogic)",
                manifestText);
        }

        [Test]
        public void RuntimeAiLabAuthoringContextBar_ReplacesLegacyToolbarAndFailsClosed()
        {
            string toolbarText = File.ReadAllText(
                "Assets/Scripts/WGameFrameworkProject/CharacterTest/UI/FairyGui/"
                + "CharacterTestFairyGuiRuntimeAiLabPanelLayoutBinder.Toolbar.cs");
            string panelText = File.ReadAllText(
                "FGUIProject/assets/MxCharacterTestRuntimeAiLabUi/RuntimeAiPlannerDevPanel.xml");
            var viewModel = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel();

            StringAssert.Contains("bool authoringContext", toolbarText);
            StringAssert.Contains("if (authoringContext && !runtimeTraceAuthoringContext)", toolbarText);
            StringAssert.Contains(
                "CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TopBar, false",
                toolbarText);
            StringAssert.Contains("ApplyAuthoringContextBarLayout", toolbarText);
            StringAssert.Contains("name=\"authoringContextBar\" xy=\"0,0\" size=\"1920,60\"", panelText);
            StringAssert.Contains("name=\"btnTestCurrentChanges\" xy=\"1330,10\" size=\"180,40\"", panelText);
            StringAssert.Contains("name=\"btnPublishCurrentChanges\" xy=\"1520,10\" size=\"180,40\"", panelText);
            Assert.IsFalse(viewModel.CanTestCurrentChanges);
            Assert.IsFalse(viewModel.CanPublishCurrentChanges);
        }

        [Test]
        public void RuntimeAiLabSetupSelectors_UseDistinctPopupHostsWithSharedContent()
        {
            string setupSelectorBinderText = File.ReadAllText(
                "Assets/Scripts/WGameFrameworkProject/CharacterTest/UI/FairyGui/"
                + "CharacterTestFairyGuiRuntimeAiLabSetupSelectorBinder.cs")
                .Replace("\r\n", "\n");

            StringAssert.Contains("_setupSelectorPopup", setupSelectorBinderText);
            StringAssert.Contains("_setupCharacterPopupHost", setupSelectorBinderText);
            StringAssert.Contains("_setupBehaviorGroupPopupHost", setupSelectorBinderText);
            StringAssert.Contains("\"setupCharacterPopover\"", setupSelectorBinderText);
            StringAssert.Contains("\"setupBehaviorGroupPopover\"", setupSelectorBinderText);
            StringAssert.Contains(
                "ShowSetupSelectorPopup(\n"
                + "                            characterSelector,\n"
                + "                            _setupCharacterPopupHost)",
                setupSelectorBinderText);
            StringAssert.Contains(
                "ShowSetupSelectorPopup(\n"
                + "                            groupSelector,\n"
                + "                            _setupBehaviorGroupPopupHost)",
                setupSelectorBinderText);
            StringAssert.Contains(
                "_setupSelectorPopup.RemoveFromParent();",
                setupSelectorBinderText);
            StringAssert.Contains(
                "popupHost.AddChild(_setupSelectorPopup);",
                setupSelectorBinderText);
            StringAssert.Contains(
                "visual.touchable = option != null;",
                setupSelectorBinderText);
            StringAssert.Contains(
                "visual?.onClick.Set(selectBehaviorGroup);",
                setupSelectorBinderText);
            StringAssert.DoesNotContain(
                "Close first so a synchronous panel refresh",
                setupSelectorBinderText);
            StringAssert.Contains(
                "DiscardSetupBehaviorGroupEditorBuffer();\n"
                + "            _commandSink?.Enqueue",
                setupSelectorBinderText);
            StringAssert.Contains(
                "SaveSetupBehaviorGroupBasicInfo",
                setupSelectorBinderText);
            StringAssert.Contains(
                "SetupBehaviorGroupPickerExpandedWidth",
                setupSelectorBinderText);
            StringAssert.Contains(
                "_setupBehaviorGroupEditorBindingInput = true;",
                setupSelectorBinderText);
            StringAssert.Contains(
                "if (_setupBehaviorGroupEditorBindingInput)\n"
                + "                    return;",
                setupSelectorBinderText);
            StringAssert.Contains(
                "if (_setupBehaviorGroupEditorForceInputRefresh\n"
                + "                || !Application.isPlaying",
                setupSelectorBinderText);
            StringAssert.Contains(
                "bool canSave = _setupBehaviorGroupEditorDirty\n"
                + "                    && TryValidateSetupBehaviorGroupEditor",
                setupSelectorBinderText);
            StringAssert.Contains(
                "_setupBehaviorGroupEditorDirty = false;",
                setupSelectorBinderText);
        }

        [Test]
        public void RuntimeAiLabPanelHost_RuntimeContractIsNonModalDebugOverlay()
        {
            var host = new GameObject("Runtime AI Lab Host Contract Test");
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost panelHost =
                host.AddComponent<CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost>();
            MethodInfo createCatalog = typeof(CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost)
                .GetMethod("CreateCatalog", BindingFlags.Instance | BindingFlags.NonPublic);
            try
            {
                Assert.IsNotNull(createCatalog);

                var catalog = (MxFairyGuiRuntimeCatalog)createCatalog.Invoke(
                    panelHost,
                    new object[] { null, MxUiNullTextProvider.Instance });

                Assert.AreEqual(2, catalog.ViewCount);
                Assert.AreEqual(2, catalog.PackageCount);
                Assert.IsTrue(catalog.TryGet(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                    out IMxFairyGuiRuntimeViewRegistration workspaceRegistration));
                Assert.IsInstanceOf<
                    MxFairyGuiRuntimeViewRegistration<
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel>>(
                    workspaceRegistration);
                Assert.IsTrue(catalog.TryGet(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.RecoveryViewId,
                    out IMxFairyGuiRuntimeViewRegistration recoveryRegistration));
                Assert.IsInstanceOf<
                    MxFairyGuiRuntimeViewRegistration<
                        CharacterTestRuntimeAiLabShellSurfaceViewModel>>(
                    recoveryRegistration);
                Assert.AreEqual(3, recoveryRegistration.Contract.Commands.Count);
                Assert.IsTrue(recoveryRegistration.Contract.Commands.All(
                    command => command.CommandId
                        == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ShellRestore
                    || command.CommandId
                        == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ShellReset
                    || command.CommandId
                        == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ShellHide));
                Assert.IsFalse(recoveryRegistration.Contract.Commands.Any(
                    command => command.CommandId
                        == CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.Start));
                Assert.AreNotEqual(
                    workspaceRegistration.ViewId,
                    recoveryRegistration.ViewId);
                Assert.AreNotEqual(
                    workspaceRegistration.Package.PackageId,
                    recoveryRegistration.Package.PackageId);
                Assert.AreEqual(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.RecoveryPackageId,
                    recoveryRegistration.Package.PackageId);
                for (int i = 0; i < catalog.Registrations.Count; i++)
                {
                    MxUiViewDescriptor descriptor = catalog.Registrations[i].Contract.Descriptor;
                    Assert.IsFalse(descriptor.Modal);
                    Assert.AreEqual(MxUiLayer.Debug, descriptor.Layer);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void RuntimeAiLabPanelHost_RecoveryPreflightIsAFullLabHardGate()
        {
            var root = new GameObject("Runtime AI Lab Preflight Gate Test");
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost host =
                root.AddComponent<CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost>();
            var snapshot = new ResourceDebugSnapshot(
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                Array.Empty<ResourceError>(),
                Array.Empty<ResourceCatalogSummary>(),
                Array.Empty<ResourceEntryOrigin>());
            var success = new MxFairyGuiRuntimeCatalogDiagnostics(
                1,
                1,
                2,
                0,
                0,
                Array.Empty<MxFairyGuiRuntimeCatalogIssue>(),
                snapshot);
            var failedRecovery = new MxFairyGuiRuntimeCatalogDiagnostics(
                1,
                1,
                2,
                0,
                1,
                Array.Empty<MxFairyGuiRuntimeCatalogIssue>(),
                snapshot);
            MethodInfo apply = typeof(CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost)
                .GetMethod("ApplyCatalogDiagnostics", BindingFlags.Instance | BindingFlags.NonPublic);
            try
            {
                Assert.IsNotNull(apply);
                Assert.IsFalse((bool)apply.Invoke(host, new object[] { success, failedRecovery }));
                Assert.IsFalse(host.IsConfigured);
                Assert.IsFalse(host.IsRecoveryConfigured);

                Assert.IsTrue((bool)apply.Invoke(host, new object[] { success, success }));
                Assert.IsTrue(host.IsConfigured);
                Assert.IsTrue(host.IsRecoveryConfigured);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RuntimeAiLabHosts_RejectDirectOpenAndCloseWhenShellLeaseIsInstalled()
        {
            var host = new GameObject("Runtime AI Lab Mount Lease Test");
            try
            {
                object lease = new object();
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost labHost =
                    host.AddComponent<CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost>();
                CharacterTestFairyGuiBehaviorDeckPanelHost deckHost =
                    host.AddComponent<CharacterTestFairyGuiBehaviorDeckPanelHost>();
                labHost.ConfigureShellMountLease(lease);
                deckHost.ConfigureShellMountLease(lease);

                Assert.IsFalse(labHost.Open());
                Assert.IsFalse(labHost.Close());
                Assert.IsFalse(labHost.Toggle());
                Assert.IsFalse(deckHost.Open());
                Assert.IsFalse(deckHost.Close());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void RuntimeAiLabSessionRecoveryFingerprint_HashesTimelineAndTypedEventContent()
        {
            var host = new GameObject("Runtime AI Lab Recovery Fingerprint Test");
            try
            {
                CharacterTestRuntimeAiPlannerDevSceneController controller =
                    host.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                controller.PrepareInitialSetupState();
                MethodInfo fingerprint = typeof(CharacterTestRuntimeAiPlannerDevSceneController)
                    .GetMethod(
                        "BuildSessionStartRecoveryFingerprint",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo timelineField = typeof(CharacterTestRuntimeAiPlannerDevSceneController)
                    .GetField("_timeline", BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo eventIndexField = typeof(CharacterTestRuntimeAiPlannerDevSceneController)
                    .GetField("_timelineEventIndex", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(fingerprint);
                Assert.IsNotNull(timelineField);
                Assert.IsNotNull(eventIndexField);

                var firstTimeline = new CharacterTestRuntimeAiPlannerDevTimeline(8);
                firstTimeline.AddRecord(
                    10,
                    1f,
                    0.1d,
                    CreateEmptyRuntimeAiPlannerSnapshot(),
                    new[]
                    {
                        new RuntimeCommand(new RuntimeFrame(10), 1, 101, 2, payload0: 7)
                    },
                    null,
                    1001L);
                timelineField.SetValue(controller, firstTimeline);
                var eventIndex = (CharacterTestRuntimeAiLabTimelineEventIndex)
                    eventIndexField.GetValue(controller);
                var identity = new CharacterTestRuntimeAiLabTimelineSessionIdentity(1, 10, 8);
                eventIndex.Reset(identity);
                eventIndex.CaptureFrame(
                    10,
                    null,
                    new[]
                    {
                        new CharacterTestRuntimeEvent(
                            new RuntimeFrame(10),
                            CharacterTestRuntimeEventType.CommandAccepted,
                            commandId: 101,
                            sourceId: 1,
                            targetId: 2)
                    },
                    null,
                    null);
                string first = (string)fingerprint.Invoke(controller, null);

                var secondTimeline = new CharacterTestRuntimeAiPlannerDevTimeline(8);
                secondTimeline.AddRecord(
                    10,
                    1f,
                    0.1d,
                    CreateEmptyRuntimeAiPlannerSnapshot(),
                    new[]
                    {
                        new RuntimeCommand(new RuntimeFrame(10), 1, 202, 2, payload0: 9)
                    },
                    null,
                    2002L);
                timelineField.SetValue(controller, secondTimeline);
                eventIndex.CaptureFrame(
                    10,
                    null,
                    new[]
                    {
                        new CharacterTestRuntimeEvent(
                            new RuntimeFrame(10),
                            CharacterTestRuntimeEventType.CommandAccepted,
                            commandId: 202,
                            sourceId: 1,
                            targetId: 2)
                    },
                    null,
                    null);
                string second = (string)fingerprint.Invoke(controller, null);

                Assert.AreEqual(firstTimeline.Count, secondTimeline.Count);
                Assert.AreEqual(1, eventIndex.CapturedFrameCount);
                Assert.AreNotEqual(first, second);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void RuntimeAiLabSessionRecoveryFingerprint_HashesPlannerCheckpointAndAnimationContent()
        {
            var host = new GameObject("Runtime AI Lab Complete Recovery Fingerprint Test");
            try
            {
                CharacterTestRuntimeAiPlannerDevSceneController controller =
                    host.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                controller.PrepareInitialSetupState();
                Type controllerType = typeof(CharacterTestRuntimeAiPlannerDevSceneController);
                MethodInfo fingerprint = controllerType.GetMethod(
                    "BuildSessionStartRecoveryFingerprint",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo frameBufferField = controllerType.GetField(
                    "_frameBuffer",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo timelineField = controllerType.GetField(
                    "_timeline",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(fingerprint);
                Assert.IsNotNull(frameBufferField);
                Assert.IsNotNull(timelineField);

                CharacterTestRuntimeAiPlannerAgentSnapshot agentA =
                    CreateRecoveryFingerprintAgent(101, "MoveToTarget");
                CharacterTestRuntimeAiPlannerAgentSnapshot agentB =
                    CreateRecoveryFingerprintAgent(202, "PrimaryAttack");
                var frameBuffer = new CharacterTestRuntimeAiPlannerDevFrameBuffer(4);
                frameBuffer.Capture(
                    12,
                    0.2f,
                    new CharacterTestRuntimeAiPlannerSnapshot(
                        true,
                        "profile-a",
                        new[] { agentA },
                        "valid-a"));
                frameBufferField.SetValue(controller, frameBuffer);
                string baseline = (string)fingerprint.Invoke(controller, null);

                frameBuffer = new CharacterTestRuntimeAiPlannerDevFrameBuffer(4);
                frameBuffer.Capture(
                    12,
                    0.2f,
                    new CharacterTestRuntimeAiPlannerSnapshot(
                        true,
                        "profile-b",
                        new[] { agentA },
                        "valid-a"));
                frameBufferField.SetValue(controller, frameBuffer);
                string profileChanged = (string)fingerprint.Invoke(controller, null);
                Assert.AreNotEqual(baseline, profileChanged);

                frameBuffer = new CharacterTestRuntimeAiPlannerDevFrameBuffer(4);
                frameBuffer.Capture(
                    12,
                    0.2f,
                    new CharacterTestRuntimeAiPlannerSnapshot(
                        true,
                        "profile-b",
                        new[] { agentA },
                        "valid-b"));
                frameBufferField.SetValue(controller, frameBuffer);
                string validationChanged = (string)fingerprint.Invoke(controller, null);
                Assert.AreNotEqual(profileChanged, validationChanged);

                frameBuffer = new CharacterTestRuntimeAiPlannerDevFrameBuffer(4);
                frameBuffer.Capture(
                    12,
                    0.2f,
                    new CharacterTestRuntimeAiPlannerSnapshot(
                        true,
                        "profile-b",
                        new[] { agentB },
                        "valid-b"));
                frameBufferField.SetValue(controller, frameBuffer);
                string agentChanged = (string)fingerprint.Invoke(controller, null);
                Assert.AreNotEqual(validationChanged, agentChanged);

                frameBufferField.SetValue(
                    controller,
                    new CharacterTestRuntimeAiPlannerDevFrameBuffer(4));
                var checkpointA = new CharacterTestAuthorityRollbackCheckpoint(
                    new RuntimeFrame(12),
                    new CharacterTestNetworkSnapshotDocument
                    {
                        SchemaVersion = CharacterTestNetworkSnapshotParticipant.SchemaVersionValue,
                        Frame = 12,
                        LocalPeerId = "peer-a"
                    });
                var checkpointB = new CharacterTestAuthorityRollbackCheckpoint(
                    new RuntimeFrame(12),
                    new CharacterTestNetworkSnapshotDocument
                    {
                        SchemaVersion = CharacterTestNetworkSnapshotParticipant.SchemaVersionValue,
                        Frame = 12,
                        LocalPeerId = "peer-b"
                    });
                var timelineA = new CharacterTestRuntimeAiPlannerDevTimeline(4);
                timelineA.AddRecord(
                    12,
                    0.2f,
                    0.2d,
                    CreateEmptyRuntimeAiPlannerSnapshot(),
                    Array.Empty<RuntimeCommand>(),
                    checkpointA,
                    77L,
                    animationInputs: new[]
                    {
                        new CharacterTestAnimationInputPresentationState(
                            1,
                            new CharacterTestCharacterAnimationComponent(
                                new Vector2(0.25f, 0.5f),
                                0.5f,
                                grounded: true,
                                airborne: false,
                                actionActive: true,
                                actionKey: "action-a"))
                    });
                timelineField.SetValue(controller, timelineA);
                string checkpointAndAnimationA = (string)fingerprint.Invoke(controller, null);

                var timelineCheckpointB = new CharacterTestRuntimeAiPlannerDevTimeline(4);
                timelineCheckpointB.AddRecord(
                    12,
                    0.2f,
                    0.2d,
                    CreateEmptyRuntimeAiPlannerSnapshot(),
                    Array.Empty<RuntimeCommand>(),
                    checkpointB,
                    77L,
                    animationInputs: timelineA.Records[0].AnimationInputs.ToArray());
                timelineField.SetValue(controller, timelineCheckpointB);
                string checkpointChanged = (string)fingerprint.Invoke(controller, null);
                Assert.AreNotEqual(checkpointAndAnimationA, checkpointChanged);

                var timelineAnimationB = new CharacterTestRuntimeAiPlannerDevTimeline(4);
                timelineAnimationB.AddRecord(
                    12,
                    0.2f,
                    0.2d,
                    CreateEmptyRuntimeAiPlannerSnapshot(),
                    Array.Empty<RuntimeCommand>(),
                    checkpointB,
                    77L,
                    animationInputs: new[]
                    {
                        new CharacterTestAnimationInputPresentationState(
                            1,
                            new CharacterTestCharacterAnimationComponent(
                                new Vector2(0.75f, 0.5f),
                                0.75f,
                                grounded: true,
                                airborne: false,
                                actionActive: true,
                                actionKey: "action-b"))
                    });
                timelineField.SetValue(controller, timelineAnimationB);
                string animationChanged = (string)fingerprint.Invoke(controller, null);
                Assert.AreNotEqual(checkpointChanged, animationChanged);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using MxFramework.Camera.URP;
using MxFramework.Runtime;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using UnityEngine;
using Fgui = global::FairyGUI;
using UnityCamera = UnityEngine.Camera;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost
    {
        private MxFairyGuiRuntimeCatalog CreateCatalog(
            IMxUiCommandSink commandSink,
            IMxUiTextProvider textProvider)
        {
            var catalog = new MxFairyGuiRuntimeCatalog();
            CopyRegistrations(CreateWorkspaceCatalog(commandSink, textProvider), catalog);
            CopyRegistrations(CreateRecoveryCatalog(commandSink), catalog);
            return catalog;
        }

        private MxFairyGuiRuntimeCatalog CreateWorkspaceCatalog(
            IMxUiCommandSink commandSink,
            IMxUiTextProvider textProvider)
        {
            MxUiViewContract contract = CharacterTestFairyGuiRuntimeAiPlannerDevPanelManifest.CreateViewContract();
            contract.Descriptor.CloseOnSceneChange = true;
            // All Lab workspaces share this published root. Hide/rebind it on navigation;
            // the owner explicitly releases it on teardown or reconfiguration.
            contract.Descriptor.KeepAlive = true;
            contract.Descriptor.Modal = false;
            contract.Commands = AddCancelCommand(contract.Commands);
            contract.DiagnosticsTags = new[]
            {
                "character-test",
                "fairygui",
                "runtime-ai-planner-dev"
            };
            MxFairyGuiPackageDescriptor package =
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelManifest.CreatePackageDescriptor();

            var catalog = new MxFairyGuiRuntimeCatalog();
            _workspaceBinder ??= new CharacterTestFairyGuiRuntimeAiPlannerDevPanelBinder(
                commandSink,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                textProvider,
                _behaviorCardPreviewHost,
                _behaviorCardWorkspaceCommands,
                _popupService);
            catalog.Register(
                new MxFairyGuiRuntimeViewRegistration
                    <CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel>(
                        contract,
                        package,
                        _workspaceBinder));
            return catalog;
        }

        internal void ConfigureBehaviorCardLibrary(
            CharacterTestRuntimeAiPlannerLocalPlayerDeckService deckService,
            Func<string, bool> openCardInLab)
        {
            ConfigureBehaviorCardWorkspaceCommands(deckService, openCardInLab);
        }

        private void ConfigureBehaviorCardWorkspaceCommands(
            CharacterTestRuntimeAiPlannerLocalPlayerDeckService deckService,
            Func<string, bool> openCardInLab)
        {
            _behaviorCardWorkspaceCommands?.Configure(
                _controller,
                deckService,
                openCardInLab,
                Refresh,
                () => _commandSink?.SelectedWorkbenchEntryIndex ?? -1,
                _commandSink != null
                    ? new Action<CharacterTestRuntimeAiLabLogicElementKind, int>(
                        _commandSink.SetBehaviorCardSelection)
                    : null,
                () => LastViewModel?.BehaviorCardWorkspace,
                libraryView: () => _controller != null && _controller.IsProjectBehaviorCardLibraryActive
                    ? _controller.CreateProjectBehaviorCardLibraryView()
                    : deckService?.Workbench?.View,
                selectHistoricalEntry: _commandSink != null
                    ? new Action<int>(_commandSink.SetHistoricalBehaviorCardEntryIndex)
                    : null,
                selectEntry: _commandSink != null
                    ? new Action<int>(_commandSink.SetWorkbenchEntryIndex)
                    : null);
        }

        private static MxFairyGuiRuntimeCatalog CreateRecoveryCatalog(
            IMxUiCommandSink commandSink)
        {
            var catalog = new MxFairyGuiRuntimeCatalog();
            MxUiViewContract recoveryContract =
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelManifest.CreateRecoveryViewContract();
            recoveryContract.Descriptor.CloseOnSceneChange = true;
            recoveryContract.Descriptor.KeepAlive = false;
            recoveryContract.Descriptor.Modal = false;
            recoveryContract.DiagnosticsTags = new[]
            {
                "character-test",
                "fairygui",
                "runtime-ai-lab-fail-safe"
            };
            catalog.Register(
                new MxFairyGuiRuntimeViewRegistration
                    <CharacterTestRuntimeAiLabShellSurfaceViewModel>(
                        recoveryContract,
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelManifest
                            .CreateRecoveryPackageDescriptor(),
                        new CharacterTestRuntimeAiLabShellSurfaceBinder(
                            commandSink,
                            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.RecoveryViewId)));
            return catalog;
        }

        private static void CopyRegistrations(
            MxFairyGuiRuntimeCatalog source,
            MxFairyGuiRuntimeCatalog destination)
        {
            for (int i = 0; i < source.Registrations.Count; i++)
                destination.Register(source.Registrations[i]);
        }

        private static IReadOnlyList<MxUiCommandDescriptor> AddCancelCommand(
            IReadOnlyList<MxUiCommandDescriptor> generatedCommands)
        {
            IReadOnlyList<MxUiCommandDescriptor> generated =
                generatedCommands ?? Array.Empty<MxUiCommandDescriptor>();
            var commands = new List<MxUiCommandDescriptor>(generated.Count + 1)
            {
                Command(MxFairyGuiCommandIds.Cancel, null)
            };
            for (int i = 0; i < generated.Count; i++)
                commands.Add(generated[i]);

            return commands;
        }

        private static MxUiCommandDescriptor Command(string commandId, Type payloadType)
        {
            return new MxUiCommandDescriptor
            {
                CommandId = commandId,
                PayloadType = payloadType != null ? payloadType.FullName : string.Empty,
                Owner = "CharacterTest"
            };
        }

        private void LogFirstDiagnostic(MxFairyGuiRuntimeCatalogDiagnostics diagnostics)
        {
            IReadOnlyList<MxFairyGuiRuntimeCatalogIssue> issues = diagnostics?.Issues;
            string message = issues != null && issues.Count > 0
                ? issues[0].Message
                : "Runtime AI Planner dev panel catalog validation failed.";
            Warn(message);
        }

        private bool ApplyCatalogDiagnostics(
            MxFairyGuiRuntimeCatalogDiagnostics workspaceDiagnostics,
            MxFairyGuiRuntimeCatalogDiagnostics recoveryDiagnostics)
        {
            bool success = workspaceDiagnostics != null
                && recoveryDiagnostics != null
                && workspaceDiagnostics.Success
                && recoveryDiagnostics.Success;
            _configured = success;
            _recoveryConfigured = success;
            if (success)
            {
                _lastStatusMessage = string.Empty;
                return true;
            }

            if (workspaceDiagnostics == null || !workspaceDiagnostics.Success)
                LogFirstDiagnostic(workspaceDiagnostics);
            else
                LogFirstDiagnostic(recoveryDiagnostics);
            return false;
        }

        private void Warn(string message)
        {
            _lastStatusMessage = message ?? string.Empty;
            _logger.Warning("CharacterTest.FairyGUI", _lastStatusMessage);
        }

        private void ClearOpenViewState()
        {
            _lastViewModelSignature = string.Empty;
            _hasLastViewModelSignature = false;
            _lastBehaviorCardWorkspaceRevision = 0L;
            _lastBehaviorCardTextSignature = default;
            _hasLastBehaviorCardWorkspaceRevision = false;
        }

        private void WriteVisibility(
            string action,
            CharacterTestRuntimeAiLabShellState state,
            string result,
            string message)
        {
            Fgui.GComponent root = RootForTests;
            CharacterTestRuntimeAiLabBehaviorRecipeEditorLog.WriteVisibility(
                "panel-host",
                action,
                string.Empty,
                state,
                _configured,
                IsWorkspaceOpen,
                root != null && !root.isDisposed,
                root != null && !root.isDisposed && root.visible,
                root != null && !root.isDisposed && root.onStage,
                result,
                message);
        }

        private Fgui.GComponent ResolveVisibleRoot(MxUiViewId viewId)
        {
            if (_layerHost == null
                || !_layerHost.TryGetVisibleComponent(viewId, out IMxFairyGuiComponentHandle component))
            {
                return null;
            }
            return (component as MxFairyGuiComponentHandle)?.Component;
        }

        private void ConfigureFairyGuiCameraStack()
        {
            if (!Application.isPlaying)
                return;

            Fgui.StageCamera.CheckMainCamera();
            UnityCamera baseCamera = UnityCamera.main;
            UnityCamera overlayCamera = Fgui.StageCamera.main;
            ExcludeFairyGuiLayerFromBaseCamera(baseCamera, overlayCamera);
            SyncFairyGuiOverlayCameraOutput(baseCamera, overlayCamera);

            MxCameraUrpStackResult result = MxCameraUrpOverlayStackBinder.Bind(
                baseCamera,
                overlayCamera,
                excludeOverlayLayersFromBaseCamera: true);
            if (!result.Success)
            {
                Warn(
                    "FairyGUI camera stack bind failed. code=" + result.Code + " message=" + result.Message);
            }
        }

        private static void SyncFairyGuiOverlayCameraOutput(UnityCamera baseCamera, UnityCamera overlayCamera)
        {
            if (baseCamera == null || overlayCamera == null)
                return;

            overlayCamera.clearFlags = CameraClearFlags.Depth;
            overlayCamera.targetTexture = baseCamera.targetTexture;
            overlayCamera.targetDisplay = baseCamera.targetDisplay;
            overlayCamera.rect = baseCamera.rect;
            overlayCamera.allowHDR = baseCamera.allowHDR;
            overlayCamera.allowMSAA = baseCamera.allowMSAA;
        }

        private static void ExcludeFairyGuiLayerFromBaseCamera(UnityCamera baseCamera, UnityCamera overlayCamera)
        {
            int uiLayer = LayerMask.NameToLayer(Fgui.StageCamera.LayerName);
            if (uiLayer < 0)
                return;

            int uiLayerMask = 1 << uiLayer;
            if (baseCamera != null)
                baseCamera.cullingMask &= ~uiLayerMask;

            if (overlayCamera != null)
                overlayCamera.cullingMask = uiLayerMask;
        }
    }
}

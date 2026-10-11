using System;
using System.Collections.Generic;
using MxFramework.Camera.URP;
using MxFramework.Core.Math;
using MxFramework.Network;
using MxFramework.Runtime;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using Unity.Profiling;
using UnityEngine;
using Fgui = global::FairyGUI;
using UnityCamera = UnityEngine.Camera;

namespace MxFramework.Game.CharacterTest
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(104)]
    [AddComponentMenu("MxFramework/Game/Character Test/FairyGUI Combat HUD Host")]
    public sealed class CharacterTestFairyGuiCombatHudHost : MonoBehaviour
    {
        private static readonly ProfilerMarker BuildViewModelMarker =
            new ProfilerMarker("CharacterTest.UI.CombatHud.BuildViewModel");
        private static readonly ProfilerMarker CreateSignatureMarker =
            new ProfilerMarker("CharacterTest.UI.CombatHud.CreateSignature");
        private static readonly ProfilerMarker OpenNavigatorMarker =
            new ProfilerMarker("CharacterTest.UI.CombatHud.OpenNavigator");
        private const float TargetLockAnchorHeightRatio = 0.62f;
        private const float TargetLockCrosshairOffscreenMargin = 64f;

        private GameSlice _slice;
        private IRuntimeLogger _logger = NullRuntimeLogger.Instance;
        private MxFairyGuiNavigator _navigator;
        private MxFairyGuiLayerHost _layerHost;
        private IMxUiTextProvider _textProvider = MxUiNullTextProvider.Instance;
        private Func<CharacterTestNetworkSessionStatus> _networkSessionStatusProvider;
        private CharacterTestRuntimeAiPlannerLocalPlayerDeckService _playerBehaviorDeckService;
        private bool _ownsLayerHost;
        private bool _configured;
        private bool _reportedOpenFailure;
        private CharacterTestFairyGuiCombatHudViewModelSignature _lastViewModelSignature;
        private CharacterTestUiTextProviderSignature _lastTextProviderSignature;
        private bool _hasLastViewModelSignature;
        private string _lastStatusMessage = string.Empty;
        private readonly CharacterTestFairyGuiCombatHudViewModel _viewModelCache =
            new CharacterTestFairyGuiCombatHudViewModel();
        private readonly CharacterTestFairyGuiCombatHudBuildBuffers _buildBuffers =
            new CharacterTestFairyGuiCombatHudBuildBuffers();
        private readonly CharacterTestAbilityHotbarSlotState[] _hotbarSlotStates =
            new CharacterTestAbilityHotbarSlotState[CharacterTestAbilityHotbarRuntime.SlotCount];
        private UnityCamera _cachedMainCamera;

        public bool IsConfigured => _configured;
        public bool IsOpen =>
            _navigator != null && _navigator.IsOpen(CharacterTestFairyGuiCombatHudIds.ViewId);
        public int RefreshAttemptCount { get; private set; }
        public int SuccessfulRefreshCount { get; private set; }
        public int SkippedRefreshCount { get; private set; }
        public string LastStatusMessage => _lastStatusMessage;
        public CharacterTestFairyGuiCombatHudViewModel LastViewModel { get; private set; }

        public void Configure(
            GameSlice slice,
            IRuntimeLogger logger = null,
            MxFairyGuiLayerHost layerHost = null,
            IMxUiTextProvider textProvider = null,
            IMxFairyGuiHost fairyGuiHost = null,
            Func<CharacterTestNetworkSessionStatus> networkSessionStatusProvider = null,
            MxFairyGuiRuntimeShell runtimeShell = null)
        {
            Release();
            _slice = slice;
            _logger = logger ?? NullRuntimeLogger.Instance;
            _textProvider = textProvider ?? MxUiNullTextProvider.Instance;
            _networkSessionStatusProvider = networkSessionStatusProvider;

            if (_slice == null || _slice.Resources?.ResourceManager == null)
            {
                Warn("Combat HUD disabled because resource runtime is not available.");
                return;
            }

            MxFairyGuiRuntimeCatalog catalog = CreateCatalog(_textProvider);
            MxFairyGuiRuntimeCatalogDiagnostics diagnostics =
                catalog.CreateDiagnostics(_slice.Resources.ResourceManager);
            if (!diagnostics.Success)
            {
                LogFirstDiagnostic(diagnostics);
                return;
            }

            _layerHost = layerHost ?? new MxFairyGuiLayerHost();
            _ownsLayerHost = layerHost == null;
            ConfigureFairyGuiCameraStack();
            _navigator = runtimeShell != null
                ? runtimeShell.Register(catalog)
                : MxFairyGuiRuntimeShellComposition.CreateNavigator(
                    _slice.Resources.ResourceManager,
                    catalog,
                    host: fairyGuiHost,
                    layerHost: _layerHost);
            _configured = true;
            _reportedOpenFailure = false;
            _lastStatusMessage = string.Empty;
            Refresh();
        }

        internal void SetPlayerBehaviorDeckService(
            CharacterTestRuntimeAiPlannerLocalPlayerDeckService service)
        {
            _playerBehaviorDeckService = service;
            ClearOpenViewState();
            Refresh();
        }

        public void Refresh()
        {
            RefreshAttemptCount++;
            if (!_configured || _navigator == null || _slice == null)
                return;

            CharacterTestFairyGuiCombatHudViewModel viewModel;
            using (BuildViewModelMarker.Auto())
            {
                viewModel = CharacterTestFairyGuiCombatHudViewModelBuilder.BuildInto(
                    _viewModelCache,
                    _slice,
                    _textProvider,
                    _networkSessionStatusProvider != null ? _networkSessionStatusProvider() : null,
                    _hotbarSlotStates,
                    _buildBuffers,
                    _playerBehaviorDeckService?.Workbench,
                    _playerBehaviorDeckService);
            }

            CharacterTestFairyGuiCombatHudViewModelSignature signature;
            CharacterTestUiTextProviderSignature textProviderSignature;
            using (CreateSignatureMarker.Auto())
            {
                signature = CharacterTestFairyGuiCombatHudViewModelSignature.Create(viewModel);
                textProviderSignature = CharacterTestUiTextProviderSignature.Create(_textProvider);
            }

            if (IsOpen &&
                _hasLastViewModelSignature &&
                _lastViewModelSignature == signature &&
                _lastTextProviderSignature == textProviderSignature)
            {
                SkippedRefreshCount++;
                LastViewModel = viewModel;
                SyncTargetLockCrosshair();
                return;
            }

            MxUiOpenResult result;
            using (OpenNavigatorMarker.Auto())
            {
                result = _navigator.Open(CharacterTestFairyGuiCombatHudIds.ViewId, viewModel);
            }

            if (result.Success)
            {
                LastViewModel = viewModel;
                _lastViewModelSignature = signature;
                _lastTextProviderSignature = textProviderSignature;
                _hasLastViewModelSignature = true;
                SuccessfulRefreshCount++;
                _reportedOpenFailure = false;
                _lastStatusMessage = string.Empty;
                SyncTargetLockCrosshair();
                return;
            }

            if (!_reportedOpenFailure)
            {
                _reportedOpenFailure = true;
                Warn("Combat HUD open failed: " + result.Message);
            }
        }

        public bool Close()
        {
            bool closed = _navigator != null && _navigator.Close(CharacterTestFairyGuiCombatHudIds.ViewId);
            ClearOpenViewState();
            return closed;
        }

        internal void SyncPresentation()
        {
            if (!_configured || !IsOpen)
                return;

            SyncTargetLockCrosshair();
        }

        public void DetachSlice()
        {
            _slice = null;
            _cachedMainCamera = null;
            _hasLastViewModelSignature = false;
            _lastViewModelSignature = default;
        }

        internal void RebindSlice(
            GameSlice slice,
            Func<CharacterTestNetworkSessionStatus> networkSessionStatusProvider = null,
            CharacterTestRuntimeAiPlannerLocalPlayerDeckService playerBehaviorDeckService = null)
        {
            _slice = slice;
            _cachedMainCamera = null;
            _hasLastViewModelSignature = false;
            _lastViewModelSignature = default;
            if (networkSessionStatusProvider != null)
                _networkSessionStatusProvider = networkSessionStatusProvider;
            if (playerBehaviorDeckService != null)
                _playerBehaviorDeckService = playerBehaviorDeckService;
            if (_configured && IsOpen)
            {
                Refresh();
            }
        }

        public void Release()
        {
            if (_navigator != null)
                _navigator.CloseSceneViews();
            ClearOpenViewState();
            if (_ownsLayerHost)
                _layerHost?.Dispose();
            _layerHost = null;
            _ownsLayerHost = false;
            _navigator = null;
            _slice = null;
            _textProvider = MxUiNullTextProvider.Instance;
            _networkSessionStatusProvider = null;
            _playerBehaviorDeckService = null;
            _cachedMainCamera = null;
            _configured = false;
            _reportedOpenFailure = false;
            _lastStatusMessage = string.Empty;
            LastViewModel = null;
            RefreshAttemptCount = 0;
            SuccessfulRefreshCount = 0;
            SkippedRefreshCount = 0;
        }

        private static MxFairyGuiRuntimeCatalog CreateCatalog(IMxUiTextProvider textProvider)
        {
            MxUiViewContract contract = CreateRuntimeViewContract();
            contract.DiagnosticsTags = new[] { "character-test", "fairygui", "combat-hud", "generated-manifest" };
            MxFairyGuiPackageDescriptor package = CharacterTestFairyGuiCombatHudManifest.CreatePackageDescriptor();

            var catalog = new MxFairyGuiRuntimeCatalog();
            catalog.Register(new MxFairyGuiRuntimeViewRegistration<CharacterTestFairyGuiCombatHudViewModel>(
                contract,
                package,
                new CharacterTestFairyGuiCombatHudBinder(
                    null,
                    CharacterTestFairyGuiCombatHudIds.ViewId,
                    textProvider)));
            return catalog;
        }

        internal static MxUiViewContract CreateRuntimeViewContract()
        {
            MxUiViewContract contract = CharacterTestFairyGuiCombatHudManifest.CreateViewContract();
            contract.Descriptor.CloseOnSceneChange = true;
            contract.Descriptor.KeepAlive = true;
            contract.Descriptor.Modal = false;
            return contract;
        }

        private void LogFirstDiagnostic(MxFairyGuiRuntimeCatalogDiagnostics diagnostics)
        {
            IReadOnlyList<MxFairyGuiRuntimeCatalogIssue> issues = diagnostics.Issues;
            string message = issues.Count > 0
                ? issues[0].Code + ": " + issues[0].Message
                : "Combat HUD FairyGUI catalog diagnostics failed.";
            _logger.Warning("CharacterTest.FairyGUI", message);
            _lastStatusMessage = message;
        }

        private void ConfigureFairyGuiCameraStack()
        {
            if (!Application.isPlaying)
                return;

            Fgui.StageCamera.CheckMainCamera();
            UnityCamera baseCamera = UnityCamera.main;
            UnityCamera overlayCamera = Fgui.StageCamera.main;
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

        private void ClearOpenViewState()
        {
            _lastViewModelSignature = default;
            _lastTextProviderSignature = default;
            _hasLastViewModelSignature = false;
        }

        private void SyncTargetLockCrosshair()
        {
            if (!TryResolveCombatHudRoot(out Fgui.GComponent root))
                return;

            bool visible = TryResolveTargetLockStagePosition(out Vector2 stagePosition);
            CharacterTestFairyGuiCombatHudBinder.SyncLockCrosshair(root, visible, stagePosition);
        }

        private bool TryResolveCombatHudRoot(out Fgui.GComponent root)
        {
            root = null;
            if (_navigator == null
                || !_navigator.TryGetOpenView(CharacterTestFairyGuiCombatHudIds.ViewId, out IMxUiView view))
            {
                return false;
            }

            var fairyGuiView = view as MxFairyGuiView<CharacterTestFairyGuiCombatHudViewModel>;
            var handle = fairyGuiView?.Component as MxFairyGuiComponentHandle;
            root = handle?.Component;
            return root != null;
        }

        private bool TryResolveTargetLockStagePosition(out Vector2 stagePosition)
        {
            stagePosition = default;
            int targetEntityId = _slice != null && _slice.TargetLockEnabled
                ? _slice.LockedTargetEntityId
                : 0;
            if (targetEntityId <= 0
                || !TryResolveTargetLockWorldAnchor(targetEntityId, out Vector3 worldAnchor))
            {
                return false;
            }

            UnityCamera camera = CharacterTestMainCameraCache.Resolve(ref _cachedMainCamera);
            return CharacterTestFloatingCombatTextProjector.TryProject(
                camera,
                worldAnchor,
                out stagePosition,
                edgePadding: 0f,
                offscreenMargin: TargetLockCrosshairOffscreenMargin);
        }

        private bool TryResolveTargetLockWorldAnchor(int entityId, out Vector3 worldAnchor)
        {
            worldAnchor = default;
            if (_slice?.Services != null
                && _slice.Services.TryGetLockedCombatTargetSample(
                    out CharacterTestLockedHurtboxDistanceSample lockedSample)
                && lockedSample.HasDistance)
            {
                worldAnchor = new Vector3(
                    (float)(lockedSample.AimPoint.X.RawValue / (double)Fix64.Scale),
                    (float)(lockedSample.AimPoint.Y.RawValue / (double)Fix64.Scale),
                    (float)(lockedSample.AimPoint.Z.RawValue / (double)Fix64.Scale));
                return true;
            }
            if (_slice?.LockedCombatTarget.HasTarget == true)
                return false;

            CharacterTestCharacterRuntimeModule characterRuntime = _slice?.Services?.CharacterRuntime;
            if (characterRuntime == null || characterRuntime.World == null)
                return false;

            CharacterTestCharacterEntity entity = characterRuntime.FindEntityById(entityId);
            if (entity == null)
                return false;

            if (characterRuntime.ViewBridge != null
                && characterRuntime.ViewBridge.TryResolveTargetLockUiAnchor(entityId, out worldAnchor))
            {
                return true;
            }

            CharacterTestCharacterTransformComponent transform = entity.Transform;
            worldAnchor = new Vector3(
                transform.X,
                transform.Y + entity.Physics.EffectiveHeightMeters * TargetLockAnchorHeightRatio,
                transform.Z);
            return true;
        }

        private void Warn(string message)
        {
            _lastStatusMessage = message ?? string.Empty;
            _logger.Warning("CharacterTest.FairyGUI", _lastStatusMessage);
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
    }
}

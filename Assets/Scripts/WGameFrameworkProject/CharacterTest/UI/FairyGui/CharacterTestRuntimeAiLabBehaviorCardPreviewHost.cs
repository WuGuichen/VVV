using System;
using MxFramework.Camera.Unity;
using MxFramework.Rendering;
using UnityEngine;
using Fgui = global::FairyGUI;
using UnityCamera = UnityEngine.Camera;
using UnityObject = UnityEngine.Object;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestRuntimeAiLabPreviewState
    {
        Dormant,
        Preparing,
        Ready,
        Suspended,
        Faulted,
        Disposed
    }

    internal readonly struct CharacterTestRuntimeAiLabPreviewResult
    {
        internal CharacterTestRuntimeAiLabPreviewResult(
            CharacterTestRuntimeAiLabPreviewState state,
            string code,
            string message,
            bool canRetry,
            int textureWidth,
            int textureHeight,
            int generation)
        {
            State = state;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            CanRetry = canRetry;
            TextureWidth = textureWidth;
            TextureHeight = textureHeight;
            Generation = generation;
        }

        internal CharacterTestRuntimeAiLabPreviewState State { get; }
        internal string Code { get; }
        internal string Message { get; }
        internal bool CanRetry { get; }
        internal int TextureWidth { get; }
        internal int TextureHeight { get; }
        internal int Generation { get; }
        internal bool Ready => State == CharacterTestRuntimeAiLabPreviewState.Ready;
    }

    internal interface ICharacterTestRuntimeAiLabBehaviorCardPreviewSurface : IDisposable
    {
        void Bind(
            Fgui.GComponent header,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel,
            bool active);

        void SetActive(bool active);
        void FitView();
        string CameraModeLabel { get; }
        void CycleCameraMode();
    }

    [DisallowMultipleComponent]
    internal sealed class CharacterTestRuntimeAiLabPreviewCameraMarker : MonoBehaviour
    {
    }

    internal sealed partial class CharacterTestRuntimeAiLabBehaviorCardPreviewHost :
        ICharacterTestRuntimeAiLabBehaviorCardPreviewSurface
    {
        internal const string SurfaceInvalidCode = "PREVIEW_SURFACE_INVALID";
        internal const string RootUnavailableCode = "PREVIEW_ROOT_UNAVAILABLE";
        internal const string SourceCameraMissingCode = "PREVIEW_SOURCE_CAMERA_MISSING";
        internal const string RenderTextureFailedCode = "PREVIEW_RENDER_TEXTURE_FAILED";
        internal const string TextureBindingInvalidCode = "PREVIEW_TEXTURE_BINDING_INVALID";
        internal const string SubjectUnavailableCode = "PREVIEW_SUBJECT_UNAVAILABLE";

        private const string LoaderName = "runtimeScenePreviewLoader";
        private const string GuideName = "scenePreviewGuide";
        private const string StateOverlayName = "previewStateOverlay";
        private const string StateTitleName = "previewStateTitle";
        private const string StateDetailName = "previewStateDetail";
        private const string RetryButtonName = "btnPreviewRetry";
        private const string HintName = "scenePreviewHint";
        private const int MinimumTextureSize = 64;
        private const int MaximumTextureSize = 2048;

        private readonly CharacterTestRuntimeAiPlannerDevSceneController _controller;
        private GameObject _cameraObject;
        private UnityCamera _previewCamera;
        private UnityCamera _sourceCamera;
        private long _sourceCameraGeneration;
        private MxUiPreviewCameraSlot _cameraSlot;
        private Fgui.GComponent _header;
        private Fgui.GObject _guide;
        private Fgui.GLoader _loader;
        private Fgui.GObject _stateOverlay;
        private Fgui.GTextField _stateTitle;
        private Fgui.GTextField _stateDetail;
        private Fgui.GButton _retryButton;
        private Fgui.GTextField _hint;
        private Fgui.NTexture _fairyTexture;
        private RenderTexture _boundRenderTexture;
        private CharacterTestRuntimeAiLabPreviewResult _result;
        private bool _active;
        private bool _fitRequested = true;
        private bool _forceRebuild;
        private bool _contractFaultPending;
        private string _contractFaultCode = string.Empty;
        private string _contractFaultMessage = string.Empty;
        private int _textureWidth;
        private int _textureHeight;
        private int _resourceGeneration;
        private readonly CharacterTestRuntimeCharacterView[] _setupCharacters =
            new CharacterTestRuntimeCharacterView[CharacterTestArenaMatchConfig.SlotsPerTeam * 2];
        private readonly int[] _setupCharacterIds = new int[CharacterTestArenaMatchConfig.SlotsPerTeam * 2];
        private readonly string[] _setupCharacterIssues = new string[CharacterTestArenaMatchConfig.SlotsPerTeam * 2];
        private object _setupResourceIdentity;
        private string _setupCharacterIssue;
        internal bool HasSetupCharacters
        {
            get
            {
                if (!_active || _controller?.IsShellSetupPhase != true) return false;
                for (int i = 0; i < _setupCharacters.Length; i++)
                    if (_setupCharacters[i]?.Instance != null) return true;
                return false;
            }
        }

        private void ReleaseSetupCharacters()
        {
            for (int i = 0; i < _setupCharacters.Length; i++)
            {
                if (_setupCharacters[i]?.Instance != null) _setupCharacters[i].Instance.SetActive(false);
                _setupCharacters[i]?.Release();
                _setupCharacters[i] = null; _setupCharacterIds[i] = 0; _setupCharacterIssues[i] = null;
            }
            _setupResourceIdentity = null;
            _setupCharacterIssue = null;
        }

        private bool RefreshSetupCharacters()
        {
            if (!_compactAuthoring || _controller?.IsShellSetupPhase != true)
            { ReleaseSetupCharacters(); return true; }
            object resources = _controller.SetupPreviewResourceIdentity;
            if (!ReferenceEquals(resources, _setupResourceIdentity))
            { ReleaseSetupCharacters(); _setupResourceIdentity = resources; }
            _setupCharacterIssue = null;
            int index = 0;
            for (int team = CharacterTestArenaMatchConfig.Team1Id; team <= CharacterTestArenaMatchConfig.Team2Id; team++)
                for (int slotIndex = 0; slotIndex < CharacterTestArenaMatchConfig.SlotsPerTeam; slotIndex++, index++)
                {
                    var slot = _controller.MatchConfig.TryGetSlot(team, slotIndex);
                    int character = slot?.Enabled == true ? slot.CharacterId : 0;
                    if (_setupCharacterIds[index] != character)
                    {
                        if (_setupCharacters[index]?.Instance != null) _setupCharacters[index].Instance.SetActive(false);
                        _setupCharacters[index]?.Release(); _setupCharacters[index] = null;
                        _setupCharacterIds[index] = character;
                        _setupCharacterIssues[index] = null;
                        if (character > 0)
                        {
                            if (!_controller.TryCreateSetupCharacterPreview(team, slotIndex, out _setupCharacters[index], out string issue))
                                _setupCharacterIssues[index] = issue;
                            _fitRequested = true;
                        }
                    }
                    if (character <= 0) continue;
                    var instance = _setupCharacters[index]?.Instance;
                    if (instance == null)
                    { _setupCharacterIssue = _setupCharacterIssues[index] ?? "角色模型尚未就位，请重试预览。"; continue; }
                    if (_controller.MatchConfig.TryGetSpawnPosition(team, slotIndex, out Vector2 position))
                        instance.transform.SetPositionAndRotation(new Vector3(position.x, 0f, position.y),
                            Quaternion.Euler(0f, team == CharacterTestArenaMatchConfig.Team1Id ? 0f : 180f, 0f));
                }
            return _setupCharacterIssue == null;
        }

        internal CharacterTestRuntimeAiLabBehaviorCardPreviewHost(
            CharacterTestRuntimeAiPlannerDevSceneController controller)
        {
            _controller = controller;
            SetResult(CharacterTestRuntimeAiLabPreviewState.Dormant, string.Empty, string.Empty, false);
        }

        internal static CharacterTestRuntimeAiLabBehaviorCardPreviewHost ActiveHost { get; private set; }
        internal bool IsActive => _active;
        internal CharacterTestRuntimeAiLabPreviewResult Result => _result;
        internal RenderTexture BoundRenderTexture => _boundRenderTexture;
        internal Fgui.GLoader BoundLoader => _loader;
        internal UnityCamera PreviewCamera => _previewCamera;
        private Fgui.GComponent _headPadLayer;

        internal Fgui.GComponent GetHeadPadLayer()
        {
            if (!_active || !IsLoaderAlive(_loader) || !_loader.onStage || _header == null) return null;
            if (_headPadLayer == null || _headPadLayer.isDisposed)
            {
                _headPadLayer = new Fgui.GComponent { name = "previewHeadPads", touchable = false };
                _header.AddChildAt(_headPadLayer, _header.GetChildIndex(_loader) + 1);
            }
            _headPadLayer.visible = _loader.visible;
            _headPadLayer.SetXY(_loader.x, _loader.y);
            _headPadLayer.SetSize(_loader.width, _loader.height);
            _headPadLayer.rootContainer.clipRect = new Rect(0, 0, _loader.width, _loader.height);
            return _headPadLayer;
        }

        private void ReleaseHeadPadLayer()
        {
            if (_headPadLayer == null) return;
            CharacterTestFairyGuiHeadPad.RestorePreviewMounts(_headPadLayer);
            if (!_headPadLayer.isDisposed) _headPadLayer.Dispose();
            _headPadLayer = null;
        }
        internal int ResourceGeneration => _resourceGeneration;
        private bool _compactAuthoring;

        public void Bind(
            Fgui.GComponent header,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel,
            bool active)
        {
            _compactAuthoring = viewModel?.BehaviorCardWorkspace?.Timeline?.CompositionEditor != null;
            if (_result.State == CharacterTestRuntimeAiLabPreviewState.Disposed)
                return;

            if (!active || !Application.isPlaying)
            {
                SetActive(false);
                return;
            }

            _active = true;
            ActiveHost = this;
            if (!ReferenceEquals(_header, header))
                AttachSurface(header);
            MountOrRefresh();
            HideCompactHint();
        }

        public void SetActive(bool active)
        {
            if (_result.State == CharacterTestRuntimeAiLabPreviewState.Disposed)
                return;

            _active = active;
            if (!active)
            {
                ReleaseSetupCharacters();
                ReleaseHeadPadLayer();
                if (ReferenceEquals(ActiveHost, this)) ActiveHost = null;
                EndNavigation();
                if (_previewCamera != null)
                    _previewCamera.enabled = false;
                if (IsLoaderAlive(_loader))
                    _loader.visible = false;
                SetStateObjectsVisible(false, false);
                SetResult(
                    CharacterTestRuntimeAiLabPreviewState.Suspended,
                    string.Empty,
                    string.Empty,
                    false);
                return;
            }

            ActiveHost = this;
            MountOrRefresh();
        }

        internal void ReleaseSuspendedResources()
        {
            if (_result.State == CharacterTestRuntimeAiLabPreviewState.Disposed)
                return;
            SetActive(false);
            ReleaseTextureTransaction();
        }

        internal CharacterTestRuntimeAiLabPreviewResult PumpActiveSurface()
        {
            if (!_active || _result.State == CharacterTestRuntimeAiLabPreviewState.Disposed)
                return _result;

            if (!IsComponentAlive(_header))
            {
                DetachSurfaceReferences(clearLoaderTexture: false);
                QueueContractFault(
                    RootUnavailableCode,
                    "Behavior Card preview root is no longer available.");
                return _result;
            }

            MountOrRefresh();
            HideCompactHint();
            return _result;
        }

        private void HideCompactHint()
        {
            if (_compactAuthoring && _hint != null)
            {
                _guide.tooltips = _hint.text;
                _hint.visible = false;
            }
        }

        internal bool TryConsumeContractFault(out string code, out string message)
        {
            code = _contractFaultCode;
            message = _contractFaultMessage;
            if (!_contractFaultPending)
                return false;

            _contractFaultPending = false;
            _contractFaultCode = string.Empty;
            _contractFaultMessage = string.Empty;
            return true;
        }

        public void FitView()
        {
            _fitRequested = true;
            if (_active)
                UpdateCameraFraming();
        }


        internal void ForceRetry()
        {
            if (_result.State == CharacterTestRuntimeAiLabPreviewState.Disposed)
                return;
            CharacterTestRuntimeAiLabCameraResolver.RequestSceneCameraRefresh();
            ReleaseSetupCharacters();
            _forceRebuild = true;
            if (_active)
                MountOrRefresh();
        }

        public void Dispose()
        {
            if (_result.State == CharacterTestRuntimeAiLabPreviewState.Disposed)
                return;

            if (ReferenceEquals(ActiveHost, this))
                ActiveHost = null;

            _active = false;
            ReleaseSetupCharacters();
            ReleaseTargetHurtboxes();
            if (_previewCamera != null)
                _previewCamera.enabled = false;
            ReleaseTextureTransaction();
            _cameraSlot?.Dispose();
            _cameraSlot = null;
            if (_cameraObject != null)
                DestroyObject(_cameraObject);
            _cameraObject = null;
            _automaticRig = null;
            _previewCamera = null;
            _sourceCamera = null;
            _sourceCameraGeneration = 0;
            DetachSurfaceReferences(clearLoaderTexture: false);
            SetResult(CharacterTestRuntimeAiLabPreviewState.Disposed, string.Empty, string.Empty, false);
        }

        private void AttachSurface(Fgui.GComponent header)
        {
            DetachSurfaceReferences(clearLoaderTexture: true);
            _header = header;
            if (!IsComponentAlive(_header))
            {
                QueueContractFault(RootUnavailableCode, "Behavior Card preview root is unavailable.");
                return;
            }

            _guide = _header.GetChild(GuideName);
            _stateOverlay = _header.GetChild(StateOverlayName);
            _stateTitle = _header.GetChild(StateTitleName)?.asTextField;
            _stateDetail = _header.GetChild(StateDetailName)?.asTextField;
            _retryButton = _header.GetChild(RetryButtonName)?.asButton;
            _hint = _header.GetChild(HintName)?.asTextField;
            if (_hint != null)
                _hint.visible = true;
            if (_stateOverlay == null || _stateTitle == null || _stateDetail == null || _retryButton == null)
            {
                QueueContractFault(
                    RootUnavailableCode,
                    "Behavior Card preview state contract is incomplete.");
                return;
            }

            _retryButton.onClick.Set(ForceRetry);
            EnsureLoader();
        }

        private void DetachSurfaceReferences(bool clearLoaderTexture)
        {
            ReleaseHeadPadLayer();
            DetachNavigation();
            if (_retryButton != null)
                _retryButton.onClick.Clear();
            if (clearLoaderTexture && IsLoaderAlive(_loader))
                _loader.texture = null;
            _header = null;
            _guide = null;
            _loader = null;
            _stateOverlay = null;
            _stateTitle = null;
            _stateDetail = null;
            _retryButton = null;
            _hint = null;
        }

        private void MountOrRefresh()
        {
            if (!_active || _result.State == CharacterTestRuntimeAiLabPreviewState.Disposed)
                return;
            if (!IsComponentAlive(_header))
            {
                QueueContractFault(RootUnavailableCode, "Behavior Card preview root is unavailable.");
                return;
            }
            if (_stateOverlay == null || _stateTitle == null || _stateDetail == null || _retryButton == null)
            {
                QueueContractFault(
                    RootUnavailableCode,
                    "Behavior Card preview state contract is incomplete.");
                return;
            }
            if (_guide == null || !EnsureLoader())
            {
                SetFault(
                    SurfaceInvalidCode,
                    "Preview viewport is not ready. Retry after the Behavior Card surface is mounted.");
                return;
            }

            SetResult(CharacterTestRuntimeAiLabPreviewState.Preparing, string.Empty, string.Empty, false);
            ShowPreparing();
            if (!CharacterTestRuntimeAiLabCameraResolver.TryGetSceneCamera(
                    out UnityCamera sourceCamera,
                    out long sourceGeneration))
            {
                SetFault(SourceCameraMissingCode, "Scene camera is unavailable.");
                return;
            }
            if (!EnsureCamera(sourceCamera, sourceGeneration))
            {
                SetFault(SourceCameraMissingCode, "Preview camera could not be prepared.");
                return;
            }

            int width = ResolveTextureSize(_loader.width);
            int height = ResolveTextureSize(_loader.height);
            if (!EnsureTextureBinding(width, height))
                return;

            if (!RefreshSetupCharacters())
            {
                SetFault(SubjectUnavailableCode, _setupCharacterIssue);
                return;
            }
            UpdateCameraFraming();
            if (!ValidateReadyBinding())
            {
                SetFault(TextureBindingInvalidCode, "Preview texture binding is invalid.");
                return;
            }

            _previewCamera.enabled = true;
            _loader.visible = true;
            SetStateObjectsVisible(false, false);
            SetResult(CharacterTestRuntimeAiLabPreviewState.Ready, string.Empty, string.Empty, false);
        }

        private bool EnsureLoader()
        {
            if (!IsComponentAlive(_header) || _guide == null)
                return false;
            if (!IsLoaderAlive(_loader) || !ReferenceEquals(_loader.parent, _header))
            {
                Fgui.GLoader existing = _header.GetChild(LoaderName)?.asLoader;
                _loader = existing ?? new Fgui.GLoader
                {
                    name = LoaderName,
                    touchable = false,
                    fill = Fgui.FillType.ScaleNoBorder,
                    align = Fgui.AlignType.Center,
                    verticalAlign = Fgui.VertAlignType.Middle
                };
                if (existing == null)
                {
                    int guideIndex = _header.GetChildIndex(_guide);
                    _header.AddChildAt(
                        _loader,
                        Math.Min(Math.Max(0, guideIndex + 1), _header.numChildren));
                }
            }

            _loader.SetXY(_guide.x + 8f, _guide.y + 8f);
            _loader.SetSize(
                Math.Max(1f, _guide.width - 16f),
                Math.Max(1f, _guide.height - 16f));
            if (_fairyTexture != null && _fairyTexture.nativeTexture == _boundRenderTexture)
                _loader.texture = _fairyTexture;
            AttachNavigation(_loader);
            return IsLoaderAlive(_loader);
        }

        private bool EnsureCamera(UnityCamera sourceCamera, long sourceGeneration)
        {
            if (sourceCamera == null)
                return false;

            bool createdCamera = _previewCamera == null;
            if (createdCamera)
            {
                _cameraObject = new GameObject("Runtime AI Lab Behavior Card Preview Camera")
                {
                    hideFlags = HideFlags.DontSave
                };
                _cameraObject.AddComponent<CharacterTestRuntimeAiLabPreviewCameraMarker>();
                _cameraObject.AddComponent<MxSceneLightingPreviewCamera>();
                _previewCamera = _cameraObject.AddComponent<UnityCamera>();
                _previewCamera.enabled = false;
                _cameraSlot = new MxUiPreviewCameraSlot(_previewCamera);
                _fitRequested = true;
            }

            bool sourceChanged = !ReferenceEquals(_sourceCamera, sourceCamera)
                || _sourceCameraGeneration != sourceGeneration;
            if (sourceChanged)
            {
                if (sourceCamera.GetComponent<CharacterTestRuntimeAiLabPreviewCameraMarker>() != null)
                    return false;
                RenderTexture target = _previewCamera.targetTexture;
                bool enabled = _previewCamera.enabled;
                Vector3 position = _previewCamera.transform.position;
                Quaternion rotation = _previewCamera.transform.rotation;
                _previewCamera.CopyFrom(sourceCamera);
                _previewCamera.targetTexture = target;
                _previewCamera.enabled = enabled;
                int uiLayer = LayerMask.NameToLayer("UI");
                if (uiLayer >= 0)
                    _previewCamera.cullingMask &= ~(1 << uiLayer);
                _previewCamera.transform.SetPositionAndRotation(
                    createdCamera ? sourceCamera.transform.position : position,
                    createdCamera ? sourceCamera.transform.rotation : rotation);
                _sourceCamera = sourceCamera;
                _sourceCameraGeneration = sourceGeneration;
                _fitRequested = true;
            }

            return true;
        }

        private bool EnsureTextureBinding(int width, int height)
        {
            RenderTexture target = _cameraSlot?.TargetTexture;
            bool rebuild = _forceRebuild
                || target == null
                || !target.IsCreated()
                || _textureWidth != width
                || _textureHeight != height
                || !ReferenceEquals(_boundRenderTexture, target)
                || _fairyTexture == null
                || _fairyTexture.nativeTexture != target
                || _previewCamera.targetTexture != target;
            _forceRebuild = false;
            if (!rebuild)
            {
                if (_loader.texture != _fairyTexture)
                    _loader.texture = _fairyTexture;
                return true;
            }

            ReleaseTextureTransaction();
            MxUiPreviewCameraSlotResult textureResult =
                _cameraSlot.EnsureTexture(width, height, 16, RenderTextureFormat.Default);
            RenderTexture replacement = _cameraSlot.TargetTexture;
            if (!textureResult.Success || replacement == null || !replacement.IsCreated())
            {
                SetFault(
                    RenderTextureFailedCode,
                    string.IsNullOrEmpty(textureResult.Message)
                        ? "Preview RenderTexture could not be created."
                        : textureResult.Message);
                return false;
            }

            _fairyTexture = new Fgui.NTexture(replacement)
            {
                destroyMethod = Fgui.DestroyMethod.None
            };
            _boundRenderTexture = replacement;
            _textureWidth = textureResult.Width;
            _textureHeight = textureResult.Height;
            _resourceGeneration++;
            _loader.texture = _fairyTexture;
            return true;
        }

        private void ReleaseTextureTransaction()
        {
            if (IsLoaderAlive(_loader))
            {
                _loader.visible = false;
                _loader.texture = null;
            }
            if (_fairyTexture != null)
            {
                _fairyTexture.destroyMethod = Fgui.DestroyMethod.None;
                _fairyTexture.Dispose();
                _fairyTexture = null;
            }
            _boundRenderTexture = null;
            _textureWidth = 0;
            _textureHeight = 0;
            _cameraSlot?.ReleaseTexture();
        }

        private bool ValidateReadyBinding()
        {
            RenderTexture target = _cameraSlot?.TargetTexture;
            return IsLoaderAlive(_loader)
                && target != null
                && target.IsCreated()
                && _previewCamera != null
                && _previewCamera.targetTexture == target
                && ReferenceEquals(_boundRenderTexture, target)
                && _fairyTexture != null
                && _fairyTexture.nativeTexture == target
                && _loader.texture == _fairyTexture;
        }

        private void UpdateCameraFraming()
        {
            if (!_active || _previewCamera == null)
                return;
            UpdateCameraModeCaption();
            if (CameraMode != CharacterTestPreviewCameraMode.Free)
            {
                // Initial framing and explicit fit requests are immediate; continuous following
                // belongs to LateUpdate so panel refresh frequency cannot make the camera jump.
                if (_fitRequested)
                    UpdateAutomaticCamera();
                return;
            }
            if (!_fitRequested)
            {
                if (_hint != null)
                {
                    _hint.visible = true;
                    _hint.text = "手动视角 · 滚轮缩放 · 右键/中键平移 · Shift+右键旋转";
                }
                return;
            }
            if (_controller == null
                || !_controller.TryBuildSelectedPreview(out CharacterTestRuntimeAiPlannerDevPreviewItem preview)
                || !preview.HasGeometry)
            {
                if (_controller != null && _controller.IsShellSetupPhase
                    && _controller.CollectPreviewCameraSubjects(_cameraSubjects, true, out _))
                {
                    Vector3 source = _cameraSubjects[0].Center;
                    Vector3 setupCenter = source;
                    for (int i = 1; i < _cameraSubjects.Count; i++) setupCenter += _cameraSubjects[i].Center;
                    setupCenter /= _cameraSubjects.Count;
                    Vector3 setupPivot = setupCenter + Vector3.up * 0.8f;
                    Vector3 setupDirection = ResolveFramingDirection(_sourceCamera);
                    Quaternion setupRotation = Quaternion.LookRotation(-setupDirection, Vector3.up);
                    float setupDistance = 5f;
                    for (int i = 0; i < _cameraSubjects.Count; i++)
                        setupDistance = Mathf.Max(setupDistance, CalculateFramingDistance(source, _cameraSubjects[i].Center,
                            setupPivot, setupRotation, _previewCamera.fieldOfView, _previewCamera.aspect));
                    _previewCamera.transform.SetPositionAndRotation(setupPivot + setupDirection * setupDistance, setupRotation);
                    if (_previewCamera.orthographic) _previewCamera.orthographicSize = Mathf.Max(3f, setupDistance * 0.5f);
                    _previewCamera.farClipPlane = Mathf.Max(_previewCamera.farClipPlane, setupDistance + 20f);
                    _navigationPivot = setupPivot;
                    _fitRequested = false;
                    if (_hint != null) { _hint.visible = true; _hint.text = "已定位配置角色的出生位置"; }
                    return;
                }
                if (_hint != null)
                {
                    _hint.visible = true;
                    _hint.text = "场景可用；当前没有可框选的 Agent。";
                }
                _fitRequested = false;
                return;
            }

            if (_hint != null)
            {
                _hint.visible = true;
                _hint.text = "视角已居中适配";
            }
            Vector3 center = preview.TargetExists
                ? (preview.SourceCenter + preview.TargetCenter) * 0.5f
                : preview.SourceCenter;
            float subjectSpan = preview.TargetExists
                ? Vector3.Distance(preview.SourceCenter, preview.TargetCenter)
                : Math.Max(2f, preview.AttackRange);
            Vector3 direction = ResolveFramingDirection(_sourceCamera);
            Vector3 pivot = center + Vector3.up * 0.8f;
            Quaternion rotation = Quaternion.LookRotation(-direction, Vector3.up);
            Vector3 target = preview.TargetExists ? preview.TargetCenter : preview.SourceCenter;
            if (_previewCamera.orthographic)
            {
                Vector3 a = Quaternion.Inverse(rotation) * (preview.SourceCenter - pivot);
                Vector3 b = Quaternion.Inverse(rotation) * (target - pivot);
                _previewCamera.orthographicSize = Mathf.Max(2.5f, Mathf.Max(
                    Mathf.Max(Mathf.Abs(a.y), Mathf.Abs(b.y)) + 2.5f,
                    (Mathf.Max(Mathf.Abs(a.x), Mathf.Abs(b.x)) + 2.5f) / Mathf.Max(0.25f, _previewCamera.aspect))) * 1.1f;
            }
            else
                _previewCamera.fieldOfView = Mathf.Clamp(_previewCamera.fieldOfView, 34f, 42f);
            float distance = CalculateFramingDistance(preview.SourceCenter, target, pivot, rotation,
                _previewCamera.fieldOfView, _previewCamera.aspect);
            _previewCamera.transform.SetPositionAndRotation(pivot + direction * distance, rotation);
            _previewCamera.farClipPlane = Mathf.Max(_previewCamera.farClipPlane, distance + subjectSpan + 10f);
            _navigationPivot = pivot;
            _fitRequested = false;
        }

        /// <remarks>NoAlloc. View-space framing of the existing preview subjects, with visual body padding.</remarks>
        internal static float CalculateFramingDistance(Vector3 source, Vector3 target, Vector3 pivot,
            Quaternion rotation, float verticalFov, float aspect)
        {
            Quaternion inverse = Quaternion.Inverse(rotation);
            Vector3 a = inverse * (source - pivot), b = inverse * (target - pivot);
            float vertical = Mathf.Tan(Mathf.Clamp(verticalFov, 10f, 100f) * Mathf.Deg2Rad * 0.5f);
            float horizontal = vertical * Mathf.Max(0.25f, aspect);
            float distance = 5f;
            for (int i = 0; i < 2; i++)
            {
                Vector3 point = i == 0 ? a : b;
                distance = Mathf.Max(distance, Mathf.Max((Mathf.Abs(point.x) + 2.5f) / horizontal - point.z,
                    (Mathf.Abs(point.y) + 2.5f) / vertical - point.z));
            }
            return distance * 1.1f;
        }

        internal static Vector3 ResolveFramingDirection(UnityCamera sourceCamera)
        {
            Vector3 direction = sourceCamera != null
                ? -sourceCamera.transform.forward
                : new Vector3(0.65f, 0.72f, -0.65f);
            if (direction.sqrMagnitude < 0.5f)
                direction = new Vector3(0.65f, 0.72f, -0.65f);
            direction.y = Mathf.Max(0.35f, direction.y);
            return direction.normalized;
        }

        private void SetFault(string code, string message)
        {
            if (_previewCamera != null)
                _previewCamera.enabled = false;
            if (IsLoaderAlive(_loader))
                _loader.visible = false;
            SetResult(CharacterTestRuntimeAiLabPreviewState.Faulted, code, message, true);
            if (_stateTitle != null)
                _stateTitle.text = "场景预览不可用";
            if (_stateDetail != null)
                _stateDetail.text = message ?? string.Empty;
            SetStateObjectsVisible(true, true);
        }

        private void QueueContractFault(string code, string message)
        {
            if (_previewCamera != null)
                _previewCamera.enabled = false;
            if (IsLoaderAlive(_loader))
                _loader.visible = false;
            _contractFaultPending = true;
            _contractFaultCode = code ?? RootUnavailableCode;
            _contractFaultMessage = message ?? "Behavior Card preview root is unavailable.";
            SetResult(
                CharacterTestRuntimeAiLabPreviewState.Faulted,
                _contractFaultCode,
                _contractFaultMessage,
                false);
        }

        private void ShowPreparing()
        {
            if (_stateTitle != null)
                _stateTitle.text = "正在建立场景预览";
            if (_stateDetail != null)
                _stateDetail.text = "正在绑定 Camera 与预览纹理…";
            SetStateObjectsVisible(true, false);
        }

        private void SetStateObjectsVisible(bool visible, bool canRetry)
        {
            if (_stateOverlay != null)
                _stateOverlay.visible = visible;
            if (_stateTitle != null)
                _stateTitle.visible = visible;
            if (_stateDetail != null)
                _stateDetail.visible = visible;
            if (_retryButton != null)
            {
                _retryButton.visible = visible && canRetry;
                _retryButton.enabled = visible && canRetry;
                _retryButton.grayed = !canRetry;
            }
        }

        private void SetResult(
            CharacterTestRuntimeAiLabPreviewState state,
            string code,
            string message,
            bool canRetry)
        {
            _result = new CharacterTestRuntimeAiLabPreviewResult(
                state,
                code,
                message,
                canRetry,
                _textureWidth,
                _textureHeight,
                _resourceGeneration);
        }

        private static int ResolveTextureSize(float logicalSize)
        {
            float scale = Math.Max(0.01f, Fgui.GRoot.contentScaleFactor);
            return Mathf.Clamp(
                Mathf.CeilToInt(Math.Max(1f, logicalSize) * scale),
                MinimumTextureSize,
                MaximumTextureSize);
        }

        private static bool IsComponentAlive(Fgui.GComponent component)
        {
            return component?.displayObject != null
                && !component.displayObject.isDisposed
                && component.displayObject.gameObject != null;
        }

        private static bool IsLoaderAlive(Fgui.GLoader loader)
        {
            return loader?.displayObject != null
                && !loader.displayObject.isDisposed
                && loader.displayObject.gameObject != null;
        }

        private static void DestroyObject(UnityObject obj)
        {
            if (obj == null)
                return;
            if (Application.isPlaying)
                UnityObject.Destroy(obj);
            else
                UnityObject.DestroyImmediate(obj);
        }
    }
}

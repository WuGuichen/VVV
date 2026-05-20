using System.Reflection;
using MxFramework.Camera.URP;
using UnityEngine;
using UnityEngine.UIElements;
using UnityCamera = UnityEngine.Camera;

namespace MxFramework.Demo.CameraUi
{
    [AddComponentMenu("MxFramework/Demo/UI Camera 3D Validation")]
    public sealed class UiCamera3DValidationDemo : MonoBehaviour
    {
        private static readonly FieldInfo DisableNoThemeWarningField =
            typeof(PanelSettings).GetField("m_DisableNoThemeWarning", BindingFlags.NonPublic | BindingFlags.Instance);

        [SerializeField] private UIDocument _document = null;
        [SerializeField] private PanelSettings _panelSettings = null;
        [SerializeField] private VisualTreeAsset _visualTree = null;
        [SerializeField] private StyleSheet _styleSheet = null;
        [SerializeField] private UnityCamera _baseCamera = null;
        [SerializeField] private UnityCamera _overlayCamera = null;
        [SerializeField] private Transform _ui3dRoot = null;
        [SerializeField] private bool _spin = true;
        [SerializeField] private float _spinDegreesPerSecond = 42f;

        private VisualElement _root;
        private Label _statusLabel;
        private Label _stackLabel;
        private Label _maskLabel;
        private Label _frameLabel;
        private Label _objectLabel;
        private Button _rebindButton;
        private Button _spinButton;
        private MxCameraUrpStackResult _lastBindResult;
        private MxCameraUrpStackResult _lastValidateResult;
        private int _frameCount;

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            EnsureDocument();
            BindAndValidate();
            RefreshUi();
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
                return;

            UnregisterCallbacks();
        }

        private void Update()
        {
            if (!Application.isPlaying)
                return;

            _frameCount++;
            if (_spin && _ui3dRoot != null)
                _ui3dRoot.Rotate(Vector3.up, _spinDegreesPerSecond * Time.deltaTime, Space.World);

            EnsureDocument();
            RefreshUi();
        }

        public void ConfigureAssets(
            UIDocument document,
            PanelSettings panelSettings,
            VisualTreeAsset visualTree,
            StyleSheet styleSheet,
            UnityCamera baseCamera,
            UnityCamera overlayCamera,
            Transform ui3dRoot)
        {
            _document = document;
            _panelSettings = panelSettings;
            _visualTree = visualTree;
            _styleSheet = styleSheet;
            _baseCamera = baseCamera;
            _overlayCamera = overlayCamera;
            _ui3dRoot = ui3dRoot;
        }

        public UiCamera3DValidationSnapshot CreateSnapshot()
        {
            int uiLayer = ResolveUiLayer();
            int uiMask = 1 << uiLayer;
            bool baseExcludesUiLayer = _baseCamera != null && (_baseCamera.cullingMask & uiMask) == 0;
            bool overlayOnlyUiLayer = _overlayCamera != null && _overlayCamera.cullingMask == uiMask;
            bool objectOnUiLayer = _ui3dRoot != null && _ui3dRoot.gameObject.layer == uiLayer;

            return new UiCamera3DValidationSnapshot(
                _lastValidateResult.Success,
                _lastValidateResult.Code,
                _lastValidateResult.StackCount,
                baseExcludesUiLayer,
                overlayOnlyUiLayer,
                objectOnUiLayer,
                _frameCount);
        }

        public UiCamera3DValidationSnapshot ValidateNow()
        {
            BindAndValidate();
            RefreshUi();
            return CreateSnapshot();
        }

        private void BindAndValidate()
        {
            ConfigureCameraMasks();
            _lastBindResult = MxCameraUrpOverlayStackBinder.Bind(_baseCamera, _overlayCamera);
            _lastValidateResult = MxCameraUrpOverlayStackBinder.ValidateBound(_baseCamera, _overlayCamera);
        }

        private void ConfigureCameraMasks()
        {
            int uiLayer = ResolveUiLayer();
            int uiMask = 1 << uiLayer;

            if (_baseCamera != null)
                _baseCamera.cullingMask &= ~uiMask;
            if (_overlayCamera != null)
                _overlayCamera.cullingMask = uiMask;
            if (_ui3dRoot != null)
                SetLayerRecursively(_ui3dRoot.gameObject, uiLayer);
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            for (int i = 0; i < root.transform.childCount; i++)
                SetLayerRecursively(root.transform.GetChild(i).gameObject, layer);
        }

        private static int ResolveUiLayer()
        {
            int layer = LayerMask.NameToLayer("UI");
            return layer >= 0 ? layer : 5;
        }

        private void EnsureDocument()
        {
            if (_document == null)
                _document = GetComponent<UIDocument>() ?? gameObject.AddComponent<UIDocument>();

            if (_document.panelSettings == null)
                _document.panelSettings = CreateRuntimePanelSettings(_panelSettings);

            if (_visualTree != null && _document.visualTreeAsset != _visualTree)
                _document.visualTreeAsset = _visualTree;

            VisualElement documentRoot = _document.rootVisualElement;
            if (documentRoot == null)
                return;

            documentRoot.style.flexGrow = 1f;
            if (_styleSheet != null && !documentRoot.styleSheets.Contains(_styleSheet))
                documentRoot.styleSheets.Add(_styleSheet);

            VisualElement nextRoot = documentRoot.Q<VisualElement>("ui-camera-validation-root");
            if (nextRoot == null)
                nextRoot = BuildFallbackTree(documentRoot);

            if (_root == nextRoot)
                return;

            UnregisterCallbacks();
            _root = nextRoot;
            CacheElements(_root);
            ApplyRuntimeTextStyles();
            RegisterCallbacks();
        }

        private VisualElement BuildFallbackTree(VisualElement documentRoot)
        {
            documentRoot.Clear();
            var root = new VisualElement { name = "ui-camera-validation-root" };
            root.AddToClassList("ui-camera-validation-root");
            documentRoot.Add(root);

            root.Add(new Label("UI Camera 3D Validation") { name = "title-label" });
            root.Add(new Label("Waiting for camera stack") { name = "status-label" });
            root.Add(new Label("-") { name = "stack-label" });
            root.Add(new Label("-") { name = "mask-label" });
            root.Add(new Label("Frame 0") { name = "frame-label" });
            root.Add(new Label("-") { name = "object-label" });

            var row = new VisualElement { name = "button-row" };
            row.AddToClassList("button-row");
            row.Add(new Button { name = "rebind-button", text = "Rebind" });
            row.Add(new Button { name = "spin-button", text = "Pause Spin" });
            root.Add(row);
            return root;
        }

        private void CacheElements(VisualElement root)
        {
            _statusLabel = root.Q<Label>("status-label");
            _stackLabel = root.Q<Label>("stack-label");
            _maskLabel = root.Q<Label>("mask-label");
            _frameLabel = root.Q<Label>("frame-label");
            _objectLabel = root.Q<Label>("object-label");
            _rebindButton = root.Q<Button>("rebind-button");
            _spinButton = root.Q<Button>("spin-button");
        }

        private void RegisterCallbacks()
        {
            if (_rebindButton != null)
                _rebindButton.clicked += OnRebindClicked;
            if (_spinButton != null)
                _spinButton.clicked += OnSpinClicked;
        }

        private void UnregisterCallbacks()
        {
            if (_rebindButton != null)
                _rebindButton.clicked -= OnRebindClicked;
            if (_spinButton != null)
                _spinButton.clicked -= OnSpinClicked;
        }

        private void OnRebindClicked()
        {
            BindAndValidate();
            RefreshUi();
        }

        private void OnSpinClicked()
        {
            _spin = !_spin;
            RefreshUi();
        }

        private void RefreshUi()
        {
            if (_root == null)
                return;

            UiCamera3DValidationSnapshot snapshot = CreateSnapshot();
            Set(_statusLabel, snapshot.StackBound ? "URP Overlay Stack Bound" : "Stack diagnostic: " + snapshot.DiagnosticCode);
            Set(_stackLabel, "Bind " + Format(_lastBindResult) + " / Validate " + Format(_lastValidateResult));
            Set(_maskLabel, "Base excludes UI layer: " + Bool(snapshot.BaseExcludesUiLayer) + " / Overlay only UI layer: " + Bool(snapshot.OverlayOnlyUiLayer));
            Set(_frameLabel, "Frame " + snapshot.FrameCount);
            Set(_objectLabel, "3D UI object on UI layer: " + Bool(snapshot.ObjectOnUiLayer) + " / spin " + (_spin ? "on" : "paused"));
            if (_spinButton != null)
                _spinButton.text = _spin ? "Pause Spin" : "Resume Spin";
        }

        private static string Format(MxCameraUrpStackResult result)
        {
            return result.Success ? "OK stack=" + result.StackCount : result.Code;
        }

        private static string Bool(bool value)
        {
            return value ? "yes" : "no";
        }

        private static void Set(Label label, string text)
        {
            if (label != null)
                label.text = text;
        }

        private void ApplyRuntimeTextStyles()
        {
            if (_root == null)
                return;

            _root.style.flexGrow = 1f;
            _root.style.position = Position.Absolute;
            _root.style.left = 0;
            _root.style.top = 0;
            _root.style.right = 0;
            _root.style.bottom = 0;
            _root.style.minHeight = 720;
            ApplyLabel(_root.Q<Label>("title-label"), 22, new Color(0.96f, 0.97f, 0.95f), FontStyle.Bold);
            ApplyLabel(_statusLabel, 15, new Color(0.73f, 1f, 0.84f), FontStyle.Bold);
            ApplyLabel(_stackLabel, 13, new Color(0.87f, 0.91f, 0.96f), FontStyle.Normal);
            ApplyLabel(_maskLabel, 13, new Color(1f, 0.89f, 0.62f), FontStyle.Normal);
            ApplyLabel(_frameLabel, 13, new Color(0.83f, 0.93f, 1f), FontStyle.Bold);
            ApplyLabel(_objectLabel, 13, new Color(0.92f, 0.89f, 1f), FontStyle.Normal);
            ApplyButton(_rebindButton);
            ApplyButton(_spinButton);
        }

        private static void ApplyLabel(Label label, int fontSize, Color color, FontStyle fontStyle)
        {
            if (label == null)
                return;

            label.style.fontSize = fontSize;
            label.style.color = color;
            label.style.unityFontStyleAndWeight = fontStyle;
        }

        private static void ApplyButton(Button button)
        {
            if (button == null)
                return;

            button.style.height = 34;
            button.style.minWidth = 120;
            button.style.marginRight = 8;
            button.style.color = Color.white;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
        }

        private static PanelSettings CreateRuntimePanelSettings(PanelSettings template)
        {
            PanelSettings settings = template != null
                ? Instantiate(template)
                : ScriptableObject.CreateInstance<PanelSettings>();

            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1280, 720);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            settings.sortingOrder = 120f;
            DisableNoThemeWarningField?.SetValue(settings, true);
            return settings;
        }
    }

    public readonly struct UiCamera3DValidationSnapshot
    {
        public UiCamera3DValidationSnapshot(
            bool stackBound,
            string diagnosticCode,
            int stackCount,
            bool baseExcludesUiLayer,
            bool overlayOnlyUiLayer,
            bool objectOnUiLayer,
            int frameCount)
        {
            StackBound = stackBound;
            DiagnosticCode = diagnosticCode ?? string.Empty;
            StackCount = stackCount;
            BaseExcludesUiLayer = baseExcludesUiLayer;
            OverlayOnlyUiLayer = overlayOnlyUiLayer;
            ObjectOnUiLayer = objectOnUiLayer;
            FrameCount = frameCount;
        }

        public bool StackBound { get; }
        public string DiagnosticCode { get; }
        public int StackCount { get; }
        public bool BaseExcludesUiLayer { get; }
        public bool OverlayOnlyUiLayer { get; }
        public bool ObjectOnUiLayer { get; }
        public int FrameCount { get; }
    }
}

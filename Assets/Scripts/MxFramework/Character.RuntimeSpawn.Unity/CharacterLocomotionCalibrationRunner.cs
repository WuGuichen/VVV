using System.Text;
using MxFramework.Animation;
using MxFramework.Resources;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.CharacterRuntimeSpawn.Unity
{
    [DefaultExecutionOrder(850)]
    [DisallowMultipleComponent]
    [AddComponentMenu("MxFramework/Character/Locomotion Calibration Runner")]
    public sealed class CharacterLocomotionCalibrationRunner : MonoBehaviour
    {
        [SerializeField] private CharacterRuntimeResourceBootstrap _bootstrap;
        [SerializeField] private bool _loadOnStart = true;
        [SerializeField] private bool _keepInputMotionEnabled = true;
        [SerializeField] private bool _showHud = true;
        [SerializeField] private PanelSettings _panelSettings;
        [SerializeField] private int _hudSortOrder = 64;

        private CharacterRuntimeInputMotionController _motionController;
        private CharacterRuntimeLocomotionBlendController _locomotionController;
        private UIDocument _hudDocument;
        private VisualElement _hudRoot;
        private Label _hudSummaryLabel;

        public CharacterRuntimeResourceBootstrap Bootstrap => _bootstrap;
        public GameObject CharacterInstance => _bootstrap != null ? _bootstrap.CharacterInstance : null;
        public CharacterRuntimeInputMotionController MotionController => _motionController;
        public CharacterRuntimeLocomotionBlendController LocomotionController => _locomotionController;
        public bool ResourceManagerReady => _bootstrap != null && _bootstrap.ResourceManager != null;
        public bool CharacterLoaded => CharacterInstance != null;
        public bool AnimationWarmupSucceeded => _bootstrap != null
            && _bootstrap.AnimationWarmupResult != null
            && _bootstrap.AnimationWarmupResult.Success;
        public int AnimationWarmupIssueCount => _bootstrap != null && _bootstrap.AnimationWarmupResult != null
            ? _bootstrap.AnimationWarmupResult.IssueCount
            : 0;
        public bool AnimationBackendReady => _locomotionController != null && _locomotionController.HasAnimationBackend;

        private void Awake()
        {
            ResolveBootstrap();
        }

        private void OnEnable()
        {
            EnsureHud();
        }

        private void Start()
        {
            ResolveBootstrap();
            if (_loadOnStart && _bootstrap != null)
                _bootstrap.LoadCharacter();
            RefreshRuntimeControllers();
            UpdateHud();
        }

        private void Update()
        {
            RefreshRuntimeControllers();
            if (_keepInputMotionEnabled && _motionController != null)
                _motionController.EnableInputMotion = true;
            UpdateHud();
        }

        private void OnDisable()
        {
            if (_hudRoot != null)
                _hudRoot.RemoveFromHierarchy();
            _hudRoot = null;
            _hudSummaryLabel = null;
        }

        public void Configure(CharacterRuntimeResourceBootstrap bootstrap, bool loadOnStart = true)
        {
            _bootstrap = bootstrap;
            _loadOnStart = loadOnStart;
            RefreshRuntimeControllers();
        }

        public string CreateHeaderSummary()
        {
            var builder = new StringBuilder();
            builder.Append("packageId: ").Append(EmptyAsDash(_bootstrap != null ? _bootstrap.PackageId : string.Empty)).Append('\n');
            builder.Append("characterResourceId: ").Append(EmptyAsDash(_bootstrap != null ? _bootstrap.CharacterResourceId : string.Empty)).Append('\n');
            builder.Append("characterInstance: ").Append(CharacterLoaded ? CharacterInstance.name : "not loaded").Append('\n');
            builder.Append("resourceManager: ").Append(ResourceManagerReady ? "ready" : "missing").Append('\n');
            builder.Append("warmup: ").Append(AnimationWarmupSucceeded ? "success" : "not ready").Append('\n');
            builder.Append("warmupIssues: ").Append(AnimationWarmupIssueCount).Append('\n');
            builder.Append("backend: ").Append(AnimationBackendReady ? "ready" : "missing").Append('\n');
            builder.Append("resourceErrors: ").Append(GetResourceErrorCount()).Append('\n');
            builder.Append("blend: ");
            if (_locomotionController != null)
            {
                builder.Append(_locomotionController.ActiveBlend2DId)
                    .Append(" x=").Append(_locomotionController.LastQuantizedBlendX)
                    .Append(" y=").Append(_locomotionController.LastQuantizedBlendY)
                    .Append(" fallback=").Append(_locomotionController.UsingFallback ? "true" : "false");
            }
            else
            {
                builder.Append("missing");
            }

            AppendResourceErrorSummary(builder);
            return builder.ToString();
        }

        private void ResolveBootstrap()
        {
            if (_bootstrap == null)
                _bootstrap = GetComponent<CharacterRuntimeResourceBootstrap>();
        }

        private void RefreshRuntimeControllers()
        {
            GameObject character = CharacterInstance;
            if (character == null)
            {
                _motionController = null;
                _locomotionController = null;
                return;
            }

            _motionController = character.GetComponentInChildren<CharacterRuntimeInputMotionController>(includeInactive: true);
            _locomotionController = character.GetComponentInChildren<CharacterRuntimeLocomotionBlendController>(includeInactive: true);
        }

        private static string EmptyAsDash(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "-" : value;
        }

        private int GetResourceErrorCount()
        {
            int count = 0;
            if (_locomotionController != null && !_locomotionController.LastAnimationResult.ResourceError.IsNone)
                count++;

            MxAnimationDiagnosticSnapshot snapshot = _locomotionController != null
                ? _locomotionController.CreateAnimationSnapshot()
                : null;
            if (snapshot != null)
                count += snapshot.RecentResourceErrors.Count;

            return count;
        }

        private void AppendResourceErrorSummary(StringBuilder builder)
        {
            bool wroteHeader = false;
            if (_locomotionController != null && !_locomotionController.LastAnimationResult.ResourceError.IsNone)
            {
                builder.Append('\n').Append("lastResourceError: ")
                    .Append(FormatResourceError(_locomotionController.LastAnimationResult.ResourceError));
                wroteHeader = true;
            }

            MxAnimationDiagnosticSnapshot snapshot = _locomotionController != null
                ? _locomotionController.CreateAnimationSnapshot()
                : null;
            if (snapshot == null || snapshot.RecentResourceErrors.Count == 0)
                return;

            if (!wroteHeader)
                builder.Append('\n');

            builder.Append('\n').Append("recentResourceErrors:");
            int max = Mathf.Min(snapshot.RecentResourceErrors.Count, 3);
            for (int i = 0; i < max; i++)
            {
                builder.Append('\n')
                    .Append("- ")
                    .Append(FormatResourceError(snapshot.RecentResourceErrors[i]));
            }
        }

        private static string FormatResourceError(ResourceError error)
        {
            if (error.IsNone)
                return "none";

            return error.Code + " key=" + error.Key + " provider=" + EmptyAsDash(error.ProviderId)
                + " message=" + EmptyAsDash(error.Message);
        }

        private void EnsureHud()
        {
            if (_hudRoot != null)
            {
                _hudRoot.style.display = _showHud ? DisplayStyle.Flex : DisplayStyle.None;
                return;
            }

            if (_hudDocument == null)
                _hudDocument = GetComponent<UIDocument>() ?? gameObject.AddComponent<UIDocument>();
            if (_hudDocument == null)
                return;

            if (_panelSettings != null)
                _hudDocument.panelSettings = _panelSettings;
            _hudDocument.sortingOrder = _hudSortOrder;
            if (_hudDocument.panelSettings == null || _hudDocument.rootVisualElement == null)
                return;

            _hudRoot = new VisualElement
            {
                name = "locomotion-calibration-header"
            };
            _hudRoot.style.position = Position.Absolute;
            _hudRoot.style.left = 16f;
            _hudRoot.style.top = 16f;
            _hudRoot.style.width = 520f;
            _hudRoot.style.maxHeight = 280f;
            _hudRoot.style.paddingLeft = 14f;
            _hudRoot.style.paddingRight = 14f;
            _hudRoot.style.paddingTop = 12f;
            _hudRoot.style.paddingBottom = 12f;
            _hudRoot.style.backgroundColor = new Color(0.035f, 0.055f, 0.075f, 0.82f);
            _hudRoot.style.borderTopLeftRadius = 6f;
            _hudRoot.style.borderTopRightRadius = 6f;
            _hudRoot.style.borderBottomLeftRadius = 6f;
            _hudRoot.style.borderBottomRightRadius = 6f;
            _hudRoot.style.borderLeftWidth = 1f;
            _hudRoot.style.borderRightWidth = 1f;
            _hudRoot.style.borderTopWidth = 1f;
            _hudRoot.style.borderBottomWidth = 1f;
            Color accent = new Color(0.1f, 0.9f, 1f, 0.9f);
            _hudRoot.style.borderLeftColor = accent;
            _hudRoot.style.borderRightColor = accent;
            _hudRoot.style.borderTopColor = accent;
            _hudRoot.style.borderBottomColor = accent;
            _hudRoot.style.display = _showHud ? DisplayStyle.Flex : DisplayStyle.None;

            var title = new Label("Locomotion Calibration")
            {
                name = "locomotion-calibration-title"
            };
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 19f;
            title.style.color = accent;
            title.style.marginBottom = 8f;

            _hudSummaryLabel = new Label(CreateHeaderSummary())
            {
                name = "locomotion-calibration-summary"
            };
            _hudSummaryLabel.style.whiteSpace = WhiteSpace.Normal;
            _hudSummaryLabel.style.fontSize = 13f;
            _hudSummaryLabel.style.color = new Color(0.92f, 0.96f, 1f, 1f);

            var help = new Label("WASD: move  |  Shift: run")
            {
                name = "locomotion-calibration-help"
            };
            help.style.marginTop = 8f;
            help.style.fontSize = 12f;
            help.style.color = new Color(0.75f, 0.83f, 0.9f, 1f);

            _hudRoot.Add(title);
            _hudRoot.Add(_hudSummaryLabel);
            _hudRoot.Add(help);
            _hudDocument.rootVisualElement.Add(_hudRoot);
        }

        private void UpdateHud()
        {
            EnsureHud();
            if (_hudSummaryLabel != null)
                _hudSummaryLabel.text = CreateHeaderSummary();
        }
    }
}

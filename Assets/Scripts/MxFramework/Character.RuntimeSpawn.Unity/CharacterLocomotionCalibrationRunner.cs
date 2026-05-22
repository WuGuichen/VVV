using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Globalization;
using MxFramework.Animation;
using MxFramework.Input;
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
        private const float BlendMapWidth = 300f;
        private const float BlendMapHeight = 220f;
        private const float BlendMapPadding = 34f;
        private const float BlendPointWidth = 64f;
        private const float BlendPointHeight = 22f;
        private const float BlendSampleSize = 18f;
        private const float BlendMarkerInset = 6f;
        private const int TrailCapacity = 72;
        private const float PresetWarmupSeconds = 0.25f;

        [SerializeField] private CharacterRuntimeResourceBootstrap _bootstrap;
        [SerializeField] private bool _loadOnStart = true;
        [SerializeField] private bool _keepInputMotionEnabled = true;
        [SerializeField] private bool _showHud = true;
        [SerializeField] private PanelSettings _panelSettings;
        [SerializeField] private int _hudSortOrder = 64;
        [SerializeField] private string _leftFootPath = string.Empty;
        [SerializeField] private string _rightFootPath = string.Empty;
        [SerializeField] private float _footContactThreshold = 0.5f;
        [SerializeField] private bool _showSceneGizmos = true;

        private CharacterRuntimeInputMotionController _motionController;
        private CharacterRuntimeLocomotionBlendController _locomotionController;
        private CharacterLocomotionFootSlipSampler _footSlipSampler;
        private CharacterLocomotionFootSlipSnapshot _footSlipSnapshot;
        private FakeInputProvider _manualInputProvider;
        private bool _manualControlEnabled;
        private Vector2 _manualDirection = Vector2.up;
        private float _manualSpeed = 1f;
        private bool _manualRun;
        private bool _labPaused;
        private bool _stepRequested;
        private float _timeScale = 1f;
        private float _previousTimeScale = 1f;
        private bool _timeScaleApplied;
        private UIDocument _hudDocument;
        private VisualElement _hudRoot;
        private VisualElement _statusRow;
        private Label _controlStateLabel;
        private Label _telemetryLabel;
        private VisualElement _blendMap;
        private Label _hudSummaryLabel;
        private Label _blendWeightsLabel;
        private Label _slipMetricsLabel;
        private Label _presetReportLabel;
        private LineRenderer _actualVelocityLine;
        private LineRenderer _nativeVelocityLine;
        private LineRenderer _leftFootTrailLine;
        private LineRenderer _rightFootTrailLine;
        private Transform _leftAnchorMarker;
        private Transform _rightAnchorMarker;
        private Material _gizmoMaterial;
        private readonly Vector3[] _leftTrail = new Vector3[TrailCapacity];
        private readonly Vector3[] _rightTrail = new Vector3[TrailCapacity];
        private int _leftTrailCount;
        private int _rightTrailCount;
        private bool _presetSequenceRunning;
        private int _presetIndex;
        private float _presetElapsedSeconds;
        private PresetAccumulator _presetAccumulator;
        private MxAnimationLocomotionPresetSequenceReport _lastPresetSequenceReport;
        private string _lastPresetReportText = string.Empty;
        private string _lastPresetReportJson = string.Empty;
        private string _lastPresetReportPath = string.Empty;
        private readonly List<MxAnimationLocomotionPresetReport> _presetReports = new List<MxAnimationLocomotionPresetReport>();

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
        public bool PresetSequenceRunning => _presetSequenceRunning;
        public MxAnimationLocomotionPresetSequenceReport LastPresetSequenceReport => _lastPresetSequenceReport;

        private static readonly LocomotionPresetDefinition[] PresetDefinitions =
        {
            new LocomotionPresetDefinition("idle", "Idle", Vector2.zero, 0f, 0f, false, 1.4f),
            new LocomotionPresetDefinition("walk_forward", "Walk Forward", Vector2.up, 0.65f, 0.65f, false, 1.8f),
            new LocomotionPresetDefinition("run_forward", "Run Forward", Vector2.up, 1f, 1f, true, 1.8f),
            new LocomotionPresetDefinition("walk_back", "Walk Back", Vector2.down, 0.65f, 0.65f, false, 1.8f),
            new LocomotionPresetDefinition("strafe_left", "Strafe Left", Vector2.left, 0.65f, 0.65f, false, 1.8f),
            new LocomotionPresetDefinition("strafe_right", "Strafe Right", Vector2.right, 0.65f, 0.65f, false, 1.8f),
            new LocomotionPresetDefinition("diagonal", "Diagonal Forward Right", new Vector2(0.7f, 0.7f), 0.75f, 0.75f, false, 1.8f),
            new LocomotionPresetDefinition("speed_ramp", "Speed Ramp", Vector2.up, 0.1f, 1f, false, 2.4f)
        };

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
            UpdateManualControl();
            UpdateHud();
        }

        private void Update()
        {
            RefreshRuntimeControllers();
            UpdatePresetDriverBeforeMotion();
            UpdateManualControl();
            UpdateHud();
            UpdatePresetProbeAfterSample();
            UpdatePresetReportHud();
            UpdateSceneGizmos();
        }

        private void OnDisable()
        {
            ReleaseManualInputProvider();
            RestoreTimeScale();
            HideSceneGizmos();
            if (_hudRoot != null)
                _hudRoot.RemoveFromHierarchy();
            _hudRoot = null;
            _blendMap = null;
            _statusRow = null;
            _controlStateLabel = null;
            _telemetryLabel = null;
            _hudSummaryLabel = null;
            _blendWeightsLabel = null;
            _slipMetricsLabel = null;
            _presetReportLabel = null;
        }

        private void OnDestroy()
        {
            ReleaseManualInputProvider();
            RestoreTimeScale();
            DestroyRuntimeObject(_actualVelocityLine != null ? _actualVelocityLine.gameObject : null);
            DestroyRuntimeObject(_nativeVelocityLine != null ? _nativeVelocityLine.gameObject : null);
            DestroyRuntimeObject(_leftFootTrailLine != null ? _leftFootTrailLine.gameObject : null);
            DestroyRuntimeObject(_rightFootTrailLine != null ? _rightFootTrailLine.gameObject : null);
            DestroyRuntimeObject(_leftAnchorMarker != null ? _leftAnchorMarker.gameObject : null);
            DestroyRuntimeObject(_rightAnchorMarker != null ? _rightAnchorMarker.gameObject : null);
            DestroyRuntimeObject(_gizmoMaterial);
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

            if (_footSlipSnapshot != null && _footSlipSnapshot.Frame != null)
            {
                builder.Append('\n').Append("slipGrade: ").Append(_footSlipSnapshot.Grade)
                    .Append(" grounded=").Append(_footSlipSnapshot.Grounded ? "true" : "false");
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
            _hudRoot.style.width = 560f;
            _hudRoot.style.maxHeight = 720f;
            _hudRoot.style.paddingLeft = 14f;
            _hudRoot.style.paddingRight = 14f;
            _hudRoot.style.paddingTop = 12f;
            _hudRoot.style.paddingBottom = 12f;
            _hudRoot.style.backgroundColor = new Color(0.03f, 0.045f, 0.06f, 0.78f);
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

            _statusRow = new VisualElement
            {
                name = "locomotion-calibration-status-row"
            };
            _statusRow.style.flexDirection = FlexDirection.Row;
            _statusRow.style.flexWrap = Wrap.Wrap;
            _statusRow.style.marginBottom = 8f;

            _hudSummaryLabel = new Label(CreateHeaderSummary())
            {
                name = "locomotion-calibration-summary"
            };
            _hudSummaryLabel.style.whiteSpace = WhiteSpace.Normal;
            _hudSummaryLabel.style.fontSize = 13f;
            _hudSummaryLabel.style.color = new Color(0.92f, 0.96f, 1f, 1f);

            VisualElement controls = CreateControlPanel();

            _telemetryLabel = new Label("Telemetry: waiting for sample")
            {
                name = "locomotion-calibration-telemetry"
            };
            StylePanel(_telemetryLabel);
            _telemetryLabel.style.fontSize = 12f;
            _telemetryLabel.style.whiteSpace = WhiteSpace.Normal;
            _telemetryLabel.style.color = new Color(0.9f, 0.96f, 1f, 1f);

            _blendMap = new VisualElement
            {
                name = "locomotion-calibration-blend-map"
            };
            _blendMap.style.position = Position.Relative;
            _blendMap.style.width = 300f;
            _blendMap.style.height = 220f;
            _blendMap.style.flexShrink = 0f;
            _blendMap.style.marginTop = 8f;
            _blendMap.style.marginBottom = 16f;
            _blendMap.style.marginRight = 12f;
            _blendMap.style.backgroundColor = new Color(0.02f, 0.035f, 0.05f, 0.92f);
            _blendMap.style.overflow = Overflow.Hidden;
            _blendMap.style.borderLeftWidth = 1f;
            _blendMap.style.borderRightWidth = 1f;
            _blendMap.style.borderTopWidth = 1f;
            _blendMap.style.borderBottomWidth = 1f;
            _blendMap.style.borderLeftColor = new Color(0.18f, 0.28f, 0.36f, 1f);
            _blendMap.style.borderRightColor = new Color(0.18f, 0.28f, 0.36f, 1f);
            _blendMap.style.borderTopColor = new Color(0.18f, 0.28f, 0.36f, 1f);
            _blendMap.style.borderBottomColor = new Color(0.18f, 0.28f, 0.36f, 1f);

            _blendWeightsLabel = new Label("Blend probe: waiting for backend")
            {
                name = "locomotion-calibration-blend-weights"
            };
            _blendWeightsLabel.style.whiteSpace = WhiteSpace.Normal;
            _blendWeightsLabel.style.fontSize = 12f;
            _blendWeightsLabel.style.color = new Color(0.84f, 0.9f, 0.96f, 1f);
            _blendWeightsLabel.style.flexGrow = 1f;
            _blendWeightsLabel.style.flexShrink = 1f;
            _blendWeightsLabel.style.marginTop = 8f;

            var blendProbeRow = new VisualElement
            {
                name = "locomotion-calibration-blend-probe"
            };
            blendProbeRow.style.flexDirection = FlexDirection.Row;
            blendProbeRow.style.alignItems = Align.FlexStart;
            blendProbeRow.style.flexShrink = 0f;

            var help = new Label("WASD/Shift: live input  |  Manual: use controls  |  Scene lines: cyan=actual, yellow=animation, red=bad foot trail")
            {
                name = "locomotion-calibration-help"
            };
            help.style.marginTop = 8f;
            help.style.fontSize = 12f;
            help.style.color = new Color(0.75f, 0.83f, 0.9f, 1f);

            _hudRoot.Add(title);
            _hudRoot.Add(_statusRow);
            _hudRoot.Add(controls);
            _hudRoot.Add(_telemetryLabel);
            _hudRoot.Add(_hudSummaryLabel);
            blendProbeRow.Add(_blendMap);
            blendProbeRow.Add(_blendWeightsLabel);
            _hudRoot.Add(blendProbeRow);

            _slipMetricsLabel = new Label("Slip metrics: waiting for character")
            {
                name = "locomotion-calibration-slip-metrics"
            };
            _slipMetricsLabel.style.whiteSpace = WhiteSpace.Normal;
            _slipMetricsLabel.style.fontSize = 12f;
            _slipMetricsLabel.style.color = new Color(0.9f, 0.94f, 0.98f, 1f);
            _slipMetricsLabel.style.marginTop = 2f;
            _slipMetricsLabel.style.marginBottom = 6f;
            _hudRoot.Add(_slipMetricsLabel);

            _presetReportLabel = new Label("Preset report: not run")
            {
                name = "locomotion-calibration-preset-report"
            };
            StylePanel(_presetReportLabel);
            _presetReportLabel.style.whiteSpace = WhiteSpace.Normal;
            _presetReportLabel.style.fontSize = 12f;
            _presetReportLabel.style.color = new Color(0.9f, 0.94f, 0.98f, 1f);
            _presetReportLabel.style.maxHeight = 160f;
            _presetReportLabel.style.marginBottom = 6f;
            _hudRoot.Add(_presetReportLabel);
            _hudRoot.Add(help);
            _hudDocument.rootVisualElement.Add(_hudRoot);
        }

        private VisualElement CreateControlPanel()
        {
            var controls = new VisualElement { name = "locomotion-calibration-controls" };
            StylePanel(controls);
            controls.style.marginBottom = 8f;

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.flexWrap = Wrap.Wrap;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 6f;

            var toggle = new Toggle("Manual")
            {
                value = _manualControlEnabled
            };
            toggle.RegisterValueChangedCallback(evt =>
            {
                _manualControlEnabled = evt.newValue;
                if (!_manualControlEnabled)
                    ReleaseManualInputProvider();
            });
            toggle.style.minWidth = 92f;
            header.Add(toggle);

            var run = new Toggle("Run")
            {
                value = _manualRun
            };
            run.RegisterValueChangedCallback(evt => _manualRun = evt.newValue);
            run.style.minWidth = 76f;
            header.Add(run);

            var pause = new Toggle("Pause")
            {
                value = _labPaused
            };
            pause.RegisterValueChangedCallback(evt => _labPaused = evt.newValue);
            pause.style.minWidth = 88f;
            header.Add(pause);

            Button step = CreateSmallButton("Step", () => _stepRequested = true);
            header.Add(step);

            Button runPresets = CreateWideButton("Run Presets", StartPresetSequence);
            header.Add(runPresets);
            Button copySummary = CreateWideButton("Copy Summary", CopyPresetReportSummary);
            header.Add(copySummary);
            Button saveJson = CreateWideButton("Save JSON", SavePresetReportJson);
            header.Add(saveJson);
            controls.Add(header);

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;

            VisualElement pad = CreateDirectionPad();
            row.Add(pad);

            var sliderColumn = new VisualElement();
            sliderColumn.style.flexGrow = 1f;
            sliderColumn.style.marginLeft = 12f;
            var speed = new Slider("Speed", 0f, 1f)
            {
                value = _manualSpeed
            };
            speed.RegisterValueChangedCallback(evt => _manualSpeed = Mathf.Clamp01(evt.newValue));
            sliderColumn.Add(speed);

            var timeScale = new Slider("Time scale", 0.1f, 1f)
            {
                value = _timeScale
            };
            timeScale.RegisterValueChangedCallback(evt => _timeScale = Mathf.Clamp(evt.newValue, 0.1f, 1f));
            sliderColumn.Add(timeScale);

            _controlStateLabel = new Label();
            _controlStateLabel.style.fontSize = 12f;
            _controlStateLabel.style.color = new Color(0.76f, 0.84f, 0.92f, 1f);
            _controlStateLabel.style.whiteSpace = WhiteSpace.Normal;
            sliderColumn.Add(_controlStateLabel);
            row.Add(sliderColumn);
            controls.Add(row);
            return controls;
        }

        private static Button CreateWideButton(string text, System.Action action)
        {
            var button = new Button(action)
            {
                text = text
            };
            button.style.height = 28f;
            button.style.minWidth = 92f;
            button.style.marginLeft = 5f;
            button.style.marginBottom = 3f;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            return button;
        }

        private VisualElement CreateDirectionPad()
        {
            var pad = new VisualElement { name = "locomotion-calibration-direction-pad" };
            pad.style.width = 122f;

            var top = new VisualElement();
            top.style.flexDirection = FlexDirection.Row;
            top.Add(CreateSmallButton("", null));
            top.Add(CreateSmallButton("↑", () => _manualDirection = Vector2.up));
            top.Add(CreateSmallButton("", null));
            var middle = new VisualElement();
            middle.style.flexDirection = FlexDirection.Row;
            middle.Add(CreateSmallButton("←", () => _manualDirection = Vector2.left));
            middle.Add(CreateSmallButton("•", () => _manualDirection = Vector2.zero));
            middle.Add(CreateSmallButton("→", () => _manualDirection = Vector2.right));
            var bottom = new VisualElement();
            bottom.style.flexDirection = FlexDirection.Row;
            bottom.Add(CreateSmallButton("", null));
            bottom.Add(CreateSmallButton("↓", () => _manualDirection = Vector2.down));
            bottom.Add(CreateSmallButton("", null));
            pad.Add(top);
            pad.Add(middle);
            pad.Add(bottom);
            return pad;
        }

        private static Button CreateSmallButton(string text, System.Action action)
        {
            var button = new Button(action)
            {
                text = text
            };
            button.style.width = 38f;
            button.style.height = 28f;
            button.style.marginRight = 3f;
            button.style.marginBottom = 3f;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            if (action == null)
            {
                button.SetEnabled(false);
                button.style.opacity = 0f;
            }
            return button;
        }

        private void UpdateManualControl()
        {
            ApplyTimeScale();
            if (_motionController == null)
            {
                ReleaseManualInputProvider();
                return;
            }

            if (_manualControlEnabled)
            {
                if (_manualInputProvider == null)
                {
                    _manualInputProvider = new FakeInputProvider();
                    _manualInputProvider.SetContext(InputContext.Gameplay);
                    _motionController.ConfigureInputProvider(_manualInputProvider);
                }

                Vector2 move = _manualDirection.sqrMagnitude > 0.0001f
                    ? _manualDirection.normalized * Mathf.Clamp01(_manualSpeed)
                    : Vector2.zero;
                _manualInputProvider.SetContext(InputContext.Gameplay);
                _manualInputProvider.SetSnapshot(CreateManualInputSnapshot(move, _manualRun));
            }
            else
            {
                ReleaseManualInputProvider();
            }

            if (_labPaused)
            {
                _motionController.EnableInputMotion = false;
                if (_stepRequested)
                {
                    _motionController.StepFrame();
                    _stepRequested = false;
                }
                return;
            }

            _motionController.EnableInputMotion = _keepInputMotionEnabled;
            if (_stepRequested)
            {
                _motionController.StepFrame();
                _stepRequested = false;
            }
        }

        private void ReleaseManualInputProvider()
        {
            if (_manualInputProvider == null)
                return;

            if (_motionController != null)
                _motionController.ConfigureInputProvider(null);
            _manualInputProvider = null;
        }

        private void ApplyTimeScale()
        {
            float next = Mathf.Clamp(_timeScale, 0.1f, 1f);
            if (Mathf.Abs(next - 1f) <= 0.001f)
            {
                RestoreTimeScale();
                return;
            }

            if (!_timeScaleApplied)
            {
                _previousTimeScale = Time.timeScale;
                _timeScaleApplied = true;
            }

            Time.timeScale = next;
        }

        private void RestoreTimeScale()
        {
            if (!_timeScaleApplied)
                return;

            Time.timeScale = _previousTimeScale;
            _timeScaleApplied = false;
        }

        private static InputSnapshot CreateManualInputSnapshot(Vector2 move, bool sprintHeld)
        {
            return new InputSnapshot(
                move,
                Vector2.zero,
                Vector2.zero,
                Vector2.zero,
                Vector2.zero,
                throttle: move.magnitude,
                jumpPressed: false,
                jumpHeld: false,
                jumpReleased: false,
                attackPrimaryPressed: false,
                attackPrimaryHeld: false,
                attackSecondaryPressed: false,
                interactPressed: false,
                dodgePressed: false,
                sprintHeld: sprintHeld,
                submitPressed: false,
                cancelPressed: false,
                pausePressed: false,
                debugTogglePressed: false);
        }

        public void StartPresetSequence()
        {
            _presetReports.Clear();
            _presetSequenceRunning = true;
            _presetIndex = 0;
            _presetElapsedSeconds = 0f;
            _presetAccumulator = null;
            _lastPresetSequenceReport = null;
            _lastPresetReportText = string.Empty;
            _lastPresetReportJson = string.Empty;
            _lastPresetReportPath = string.Empty;
            _labPaused = false;
            _manualControlEnabled = true;
            BeginCurrentPreset();
            UpdatePresetReportHud();
        }

        private void StopPresetSequence()
        {
            _presetSequenceRunning = false;
            _presetAccumulator = null;
        }

        private void BeginCurrentPreset()
        {
            if (_presetIndex < 0 || _presetIndex >= PresetDefinitions.Length)
            {
                CompletePresetSequence();
                return;
            }

            LocomotionPresetDefinition preset = PresetDefinitions[_presetIndex];
            _presetElapsedSeconds = 0f;
            _presetAccumulator = new PresetAccumulator(preset);
            ApplyPresetControl(preset, 0f);
        }

        private void UpdatePresetDriverBeforeMotion()
        {
            if (!_presetSequenceRunning)
                return;

            if (_presetIndex < 0 || _presetIndex >= PresetDefinitions.Length)
            {
                CompletePresetSequence();
                return;
            }

            LocomotionPresetDefinition preset = PresetDefinitions[_presetIndex];
            float progress = preset.DurationSeconds <= 0f
                ? 1f
                : Mathf.Clamp01(Mathf.Max(0f, _presetElapsedSeconds - PresetWarmupSeconds) / preset.DurationSeconds);
            ApplyPresetControl(preset, progress);
        }

        private void ApplyPresetControl(LocomotionPresetDefinition preset, float progress)
        {
            Vector2 direction = preset.Direction.sqrMagnitude > 0.0001f ? preset.Direction.normalized : Vector2.zero;
            _manualControlEnabled = true;
            _manualDirection = direction;
            _manualSpeed = Mathf.Clamp01(Mathf.Lerp(preset.StartSpeed, preset.EndSpeed, Mathf.Clamp01(progress)));
            _manualRun = preset.Run;
        }

        private void UpdatePresetProbeAfterSample()
        {
            if (!_presetSequenceRunning)
                return;

            if (_presetIndex < 0 || _presetIndex >= PresetDefinitions.Length)
            {
                CompletePresetSequence();
                return;
            }

            LocomotionPresetDefinition preset = PresetDefinitions[_presetIndex];
            _presetElapsedSeconds += Mathf.Max(0f, Time.deltaTime);

            bool ready = CharacterLoaded && AnimationWarmupSucceeded && AnimationBackendReady;
            if (ready && _presetElapsedSeconds >= PresetWarmupSeconds && _footSlipSnapshot != null)
            {
                MxAnimationLocomotionBlendProbeSnapshot probe = _locomotionController != null
                    ? _locomotionController.CreateLocomotionBlendProbeSnapshot()
                    : null;
                _presetAccumulator?.AddSample(
                    _footSlipSnapshot,
                    probe,
                    GetResourceErrorCount(),
                    AnimationBackendReady ? 0 : 1,
                    CalculateSuggestedPlaybackSpeed(_footSlipSnapshot.Frame));
            }
            else if (!ready)
            {
                _presetAccumulator?.AddDiagnostic("Waiting for character, warmup, or animation backend.");
            }

            if (_presetElapsedSeconds < preset.DurationSeconds + PresetWarmupSeconds)
                return;

            _presetReports.Add(_presetAccumulator != null
                ? _presetAccumulator.CreateReport()
                : CreateEmptyPresetReport(preset, "Preset did not initialize."));
            _presetIndex++;
            BeginCurrentPreset();
        }

        private void CompletePresetSequence()
        {
            _presetSequenceRunning = false;
            _presetAccumulator = null;
            _lastPresetSequenceReport = new MxAnimationLocomotionPresetSequenceReport(
                _bootstrap != null ? _bootstrap.PackageId : string.Empty,
                _bootstrap != null ? _bootstrap.CharacterResourceId : string.Empty,
                _bootstrap != null && _bootstrap.RuntimeAnimationSetDefinition != null
                    ? _bootstrap.RuntimeAnimationSetDefinition.SetId
                    : string.Empty,
                _locomotionController != null ? _locomotionController.ActiveBlend2DId : string.Empty,
                DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                _presetReports);
            _lastPresetReportText = MxAnimationLocomotionCalibrationReportFormatter.CreateSummary(_lastPresetSequenceReport);
            _lastPresetReportJson = MxAnimationLocomotionCalibrationReportFormatter.CreateJson(_lastPresetSequenceReport);
            _manualControlEnabled = false;
            ReleaseManualInputProvider();
        }

        private static MxAnimationLocomotionPresetReport CreateEmptyPresetReport(
            LocomotionPresetDefinition preset,
            string diagnostic)
        {
            return new MxAnimationLocomotionPresetReport(
                preset.Id,
                preset.DisplayName,
                0f,
                0,
                0f,
                0f,
                0f,
                MxAnimationFootSlipGrade.Bad,
                string.Empty,
                0,
                0,
                1,
                0f,
                new[] { diagnostic },
                new[] { "runtime.backend" });
        }

        private void CopyPresetReportSummary()
        {
            if (string.IsNullOrEmpty(_lastPresetReportText))
                _lastPresetReportText = _lastPresetSequenceReport != null
                    ? MxAnimationLocomotionCalibrationReportFormatter.CreateSummary(_lastPresetSequenceReport)
                    : "Preset report has not run.";
            GUIUtility.systemCopyBuffer = _lastPresetReportText;
        }

        private void SavePresetReportJson()
        {
            if (string.IsNullOrEmpty(_lastPresetReportJson))
                _lastPresetReportJson = _lastPresetSequenceReport != null
                    ? MxAnimationLocomotionCalibrationReportFormatter.CreateJson(_lastPresetSequenceReport)
                    : "{}";

            string fileName = "locomotion_calibration_"
                + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)
                + ".json";
            string path = Path.Combine(Application.persistentDataPath, fileName);
            try
            {
                File.WriteAllText(path, _lastPresetReportJson, Encoding.UTF8);
                _lastPresetReportPath = path;
            }
            catch (Exception ex)
            {
                _lastPresetReportPath = "save failed: " + ex.Message;
            }
        }

        private void UpdatePresetReportHud()
        {
            if (_presetReportLabel == null)
                return;

            if (_presetSequenceRunning)
            {
                string presetName = _presetIndex >= 0 && _presetIndex < PresetDefinitions.Length
                    ? PresetDefinitions[_presetIndex].DisplayName
                    : "Complete";
                _presetReportLabel.text = "Preset report: running "
                    + (_presetIndex + 1).ToString(CultureInfo.InvariantCulture)
                    + "/" + PresetDefinitions.Length.ToString(CultureInfo.InvariantCulture)
                    + " " + presetName
                    + " t=" + FormatFloat(_presetElapsedSeconds)
                    + " samples=" + (_presetAccumulator != null ? _presetAccumulator.SampleCount : 0);
                return;
            }

            if (_lastPresetSequenceReport == null)
            {
                _presetReportLabel.text = "Preset report: not run";
                return;
            }

            var builder = new StringBuilder();
            builder.Append("Preset report: ").Append(_lastPresetSequenceReport.OverallGrade)
                .Append(" presets=").Append(_lastPresetSequenceReport.Presets.Count);
            if (!string.IsNullOrWhiteSpace(_lastPresetReportPath))
                builder.Append('\n').Append(_lastPresetReportPath);
            int count = Mathf.Min(_lastPresetSequenceReport.Presets.Count, 4);
            for (int i = 0; i < count; i++)
            {
                MxAnimationLocomotionPresetReport preset = _lastPresetSequenceReport.Presets[i];
                builder.Append('\n')
                    .Append(preset.DisplayName)
                    .Append(" ").Append(preset.Grade)
                    .Append(" err=").Append(FormatFloat(preset.AverageVelocityErrorRatio))
                    .Append(" slip=").Append(FormatFloat(preset.MaxSlipDistanceCm))
                    .Append(" dom=").Append(EmptyAsDash(preset.DominantClipId));
            }

            _presetReportLabel.text = builder.ToString();
        }

        private static void StylePanel(VisualElement element)
        {
            element.style.paddingLeft = 10f;
            element.style.paddingRight = 10f;
            element.style.paddingTop = 8f;
            element.style.paddingBottom = 8f;
            element.style.borderTopLeftRadius = 4f;
            element.style.borderTopRightRadius = 4f;
            element.style.borderBottomLeftRadius = 4f;
            element.style.borderBottomRightRadius = 4f;
            element.style.backgroundColor = new Color(0.02f, 0.032f, 0.045f, 0.72f);
            element.style.borderLeftWidth = 1f;
            element.style.borderRightWidth = 1f;
            element.style.borderTopWidth = 1f;
            element.style.borderBottomWidth = 1f;
            Color border = new Color(0.15f, 0.24f, 0.31f, 0.92f);
            element.style.borderLeftColor = border;
            element.style.borderRightColor = border;
            element.style.borderTopColor = border;
            element.style.borderBottomColor = border;
        }

        private void UpdateHud()
        {
            EnsureHud();
            UpdateFootSlipSnapshot();
            UpdateStatusBadges();
            UpdateTelemetryHud();
            UpdateControlStateHud();
            if (_hudSummaryLabel != null)
                _hudSummaryLabel.text = CreateHeaderSummary();
            UpdateBlendProbeHud();
            if (_slipMetricsLabel != null)
                _slipMetricsLabel.text = CreateSlipMetricsSummary(_footSlipSnapshot);
        }

        private void UpdateStatusBadges()
        {
            if (_statusRow == null)
                return;

            _statusRow.Clear();
            AddBadge(_statusRow, "Package", EmptyAsDash(_bootstrap != null ? _bootstrap.PackageId : string.Empty), _bootstrap != null);
            AddBadge(_statusRow, "Character", CharacterLoaded ? "loaded" : "missing", CharacterLoaded);
            AddBadge(_statusRow, "Warmup", AnimationWarmupSucceeded ? "ready" : "waiting", AnimationWarmupSucceeded);
            AddBadge(_statusRow, "Backend", AnimationBackendReady ? "ready" : "missing", AnimationBackendReady);
            bool slipOk = _footSlipSnapshot == null || _footSlipSnapshot.Grade == MxAnimationFootSlipGrade.Ok;
            AddBadge(_statusRow, "Slip", _footSlipSnapshot != null ? _footSlipSnapshot.Grade.ToString() : "waiting", slipOk, _footSlipSnapshot != null && _footSlipSnapshot.Grade == MxAnimationFootSlipGrade.Warning);
        }

        private static void AddBadge(VisualElement row, string label, string value, bool ok, bool warning = false)
        {
            var badge = new Label(label + ": " + value);
            badge.style.fontSize = 11f;
            badge.style.unityFontStyleAndWeight = FontStyle.Bold;
            badge.style.color = Color.white;
            badge.style.paddingLeft = 8f;
            badge.style.paddingRight = 8f;
            badge.style.paddingTop = 4f;
            badge.style.paddingBottom = 4f;
            badge.style.marginRight = 5f;
            badge.style.marginBottom = 5f;
            badge.style.borderTopLeftRadius = 10f;
            badge.style.borderTopRightRadius = 10f;
            badge.style.borderBottomLeftRadius = 10f;
            badge.style.borderBottomRightRadius = 10f;
            badge.style.backgroundColor = warning
                ? new Color(0.78f, 0.48f, 0.08f, 0.92f)
                : ok ? new Color(0.06f, 0.46f, 0.38f, 0.92f) : new Color(0.62f, 0.16f, 0.14f, 0.92f);
            row.Add(badge);
        }

        private void UpdateControlStateHud()
        {
            if (_controlStateLabel == null)
                return;

            _controlStateLabel.text = "manual=" + (_manualControlEnabled ? "on" : "off")
                + " dir=(" + FormatFloat(_manualDirection.x) + "," + FormatFloat(_manualDirection.y) + ")"
                + " speed=" + FormatFloat(_manualSpeed)
                + " run=" + (_manualRun ? "on" : "off")
                + " paused=" + (_labPaused ? "yes" : "no")
                + " timeScale=" + FormatFloat(_timeScale);
        }

        private void UpdateTelemetryHud()
        {
            if (_telemetryLabel == null)
                return;

            if (_footSlipSnapshot == null || _footSlipSnapshot.Frame == null)
            {
                _telemetryLabel.text = "Telemetry: waiting for sample";
                return;
            }

            MxAnimationLocomotionCalibrationFrame frame = _footSlipSnapshot.Frame;
            _telemetryLabel.text =
                "Velocity  actual=(" + FormatFloat(frame.ActualLocalVelocityX) + ", " + FormatFloat(frame.ActualLocalVelocityY) + ")"
                + "  blended=(" + FormatFloat(frame.BlendedNativeVelocityX) + ", " + FormatFloat(frame.BlendedNativeVelocityY) + ")"
                + "  error=" + FormatFloat(frame.VelocityErrorRatio)
                + "  dirErr=" + FormatFloat(frame.DirectionErrorDegrees) + " deg"
                + "\nDominant  " + EmptyAsDash(frame.DominantClipId)
                + "  suggestedSpeed=" + FormatFloat(CalculateSuggestedPlaybackSpeed(frame));
        }

        private static float CalculateSuggestedPlaybackSpeed(MxAnimationLocomotionCalibrationFrame frame)
        {
            float actual = Mathf.Sqrt(
                (frame.ActualLocalVelocityX * frame.ActualLocalVelocityX)
                + (frame.ActualLocalVelocityY * frame.ActualLocalVelocityY));
            float native = Mathf.Sqrt(
                (frame.BlendedNativeVelocityX * frame.BlendedNativeVelocityX)
                + (frame.BlendedNativeVelocityY * frame.BlendedNativeVelocityY));
            if (actual <= 0.0001f && native <= 0.0001f)
                return 1f;
            if (native <= 0.0001f)
                return 0f;
            return Mathf.Clamp(actual / native, 0f, 3f);
        }

        private void UpdateFootSlipSnapshot()
        {
            if (_footSlipSampler == null)
                _footSlipSampler = new CharacterLocomotionFootSlipSampler(
                    MxAnimationFootSlipThresholds.Default,
                    _footContactThreshold);

            GameObject character = CharacterInstance;
            if (character == null || _locomotionController == null)
            {
                _footSlipSnapshot = null;
                _footSlipSampler.Reset();
                return;
            }

            Animator animator = character.GetComponentInChildren<Animator>(includeInactive: true);
            MxAnimationLocomotionBlendProbeSnapshot probe = _locomotionController.CreateLocomotionBlendProbeSnapshot();
            MxAnimationDiagnosticSnapshot animationSnapshot = _locomotionController.CreateAnimationSnapshot();
            MxAnimationSetDefinition definition = _bootstrap != null ? _bootstrap.RuntimeAnimationSetDefinition : null;
            bool grounded = _motionController == null || !_motionController.IsInitialized || _motionController.LastMotionResult.Grounded;
            _footSlipSnapshot = _footSlipSampler.Sample(
                _motionController != null ? _motionController.CurrentFrame : Time.frameCount,
                Time.deltaTime,
                character.transform,
                animator,
                definition,
                probe,
                animationSnapshot,
                grounded,
                _leftFootPath,
                _rightFootPath);
        }

        private void UpdateBlendProbeHud()
        {
            if (_blendMap == null || _blendWeightsLabel == null)
                return;

            _blendMap.Clear();
            MxAnimationLocomotionBlendProbeSnapshot probe = _locomotionController != null
                ? _locomotionController.CreateLocomotionBlendProbeSnapshot()
                : null;
            if (probe == null)
            {
                _blendWeightsLabel.text = "Blend probe: no active 2D blend definition";
                return;
            }

            DrawAxis(_blendMap, probe, horizontal: true);
            DrawAxis(_blendMap, probe, horizontal: false);
            DrawBlendPoints(_blendMap, probe);
            DrawSample(_blendMap, probe);
            _blendWeightsLabel.text = CreateBlendProbeSummary(probe);
        }

        private static void DrawAxis(VisualElement map, MxAnimationLocomotionBlendProbeSnapshot probe, bool horizontal)
        {
            Vector2 origin = MapPoint(probe.Domain, 0, 0);
            var axis = new VisualElement
            {
                name = horizontal ? "blend-map-axis-x" : "blend-map-axis-y"
            };
            axis.style.position = Position.Absolute;
            axis.style.backgroundColor = new Color(0.2f, 0.34f, 0.44f, 0.85f);
            if (horizontal)
            {
                axis.style.left = 0f;
                axis.style.right = 0f;
                axis.style.top = origin.y;
                axis.style.height = 1f;
            }
            else
            {
                axis.style.left = origin.x;
                axis.style.top = 0f;
                axis.style.bottom = 0f;
                axis.style.width = 1f;
            }

            map.Add(axis);
        }

        private static void DrawBlendPoints(VisualElement map, MxAnimationLocomotionBlendProbeSnapshot probe)
        {
            MxAnimationBlendReachabilityReport report = probe.ReachabilityReport;
            if (report == null)
                return;

            for (int i = 0; i < report.ReachablePoints.Count; i++)
                DrawPointMarker(map, probe, report.ReachablePoints[i], false);
            for (int i = 0; i < report.UnreachablePoints.Count; i++)
                DrawPointMarker(map, probe, report.UnreachablePoints[i], true);
        }

        private static void DrawPointMarker(
            VisualElement map,
            MxAnimationLocomotionBlendProbeSnapshot probe,
            MxAnimationBlendReachabilityPoint point,
            bool unreachable)
        {
            Vector2 position = MapPoint(probe.Domain, point.X, point.Y);
            var marker = new Label(ShortClipName(point.ClipKey))
            {
                name = unreachable ? "blend-point-unreachable" : "blend-point"
            };
            marker.style.position = Position.Absolute;
            marker.style.left = Clamp(position.x - (BlendPointWidth * 0.5f), BlendMarkerInset, BlendMapWidth - BlendPointWidth - BlendMarkerInset);
            marker.style.top = Clamp(position.y - (BlendPointHeight * 0.5f), BlendMarkerInset, BlendMapHeight - BlendPointHeight - BlendMarkerInset);
            marker.style.width = BlendPointWidth;
            marker.style.height = BlendPointHeight;
            marker.style.unityTextAlign = TextAnchor.MiddleCenter;
            marker.style.whiteSpace = WhiteSpace.NoWrap;
            marker.style.fontSize = 8f;
            marker.style.color = Color.white;
            marker.style.backgroundColor = unreachable
                ? new Color(0.9f, 0.34f, 0.08f, 0.88f)
                : new Color(0.07f, 0.54f, 0.62f, 0.9f);
            marker.tooltip = point.ClipKey + " point=(" + point.X + "," + point.Y + ")"
                + (unreachable ? " unreachable" : " reachable");
            map.Add(marker);
        }

        private static void DrawSample(VisualElement map, MxAnimationLocomotionBlendProbeSnapshot probe)
        {
            Vector2 position = MapPoint(probe.Domain, probe.SampleX, probe.SampleY);
            var marker = new Label("+")
            {
                name = "blend-current-sample"
            };
            marker.style.position = Position.Absolute;
            marker.style.left = Clamp(position.x - (BlendSampleSize * 0.5f), BlendMarkerInset, BlendMapWidth - BlendSampleSize - BlendMarkerInset);
            marker.style.top = Clamp(position.y - (BlendSampleSize * 0.5f), BlendMarkerInset, BlendMapHeight - BlendSampleSize - BlendMarkerInset);
            marker.style.width = BlendSampleSize;
            marker.style.height = BlendSampleSize;
            marker.style.unityTextAlign = TextAnchor.MiddleCenter;
            marker.style.fontSize = 16f;
            marker.style.unityFontStyleAndWeight = FontStyle.Bold;
            marker.style.color = new Color(1f, 0.95f, 0.2f, 1f);
            marker.tooltip = "sample=(" + probe.SampleX + "," + probe.SampleY + ")";
            map.Add(marker);
        }

        private static string CreateBlendProbeSummary(MxAnimationLocomotionBlendProbeSnapshot probe)
        {
            var builder = new StringBuilder();
            builder.Append("Blend probe: ").Append(probe.BlendId)
                .Append(" weights=").Append(probe.WeightsFromBackend ? "backend" : "calculated")
                .Append('\n');
            builder.Append("domain: x=[").Append(probe.Domain.MinX).Append(',').Append(probe.Domain.MaxX)
                .Append("] y=[").Append(probe.Domain.MinY).Append(',').Append(probe.Domain.MaxY)
                .Append("]  sample=(").Append(probe.SampleX).Append(',').Append(probe.SampleY).Append(')')
                .Append('\n');

            MxAnimationBlendReachabilityReport report = probe.ReachabilityReport;
            if (report != null)
            {
                builder.Append("points reachable=").Append(report.ReachablePoints.Count)
                    .Append(" unreachable=").Append(report.UnreachablePoints.Count).Append('\n');
                for (int i = 0; i < report.Issues.Count; i++)
                {
                    MxAnimationBlendReachabilityIssue issue = report.Issues[i];
                    builder.Append(issue.Code)
                        .Append(" clip=").Append(ShortClipName(issue.ClipKey))
                        .Append(" point=(").Append(issue.X).Append(',').Append(issue.Y).Append(')')
                        .Append('\n');
                }
            }

            builder.Append("dominant: ");
            if (probe.HasDominantClip)
            {
                builder.Append(ShortClipName(probe.DominantClipKey))
                    .Append(" weight=").Append(FormatFloat(probe.DominantWeight));
            }
            else
            {
                builder.Append("-");
            }

            builder.Append('\n').Append("weights:");
            for (int i = 0; i < probe.Weights.Count; i++)
            {
                MxAnimationBlend2DWeight weight = probe.Weights[i];
                builder.Append('\n')
                    .Append("- ").Append(ShortClipName(weight.ClipKey))
                    .Append(" point=(").Append(weight.X).Append(',').Append(weight.Y).Append(')')
                    .Append(" w=").Append(FormatFloat(weight.Weight));
            }

            return builder.ToString();
        }

        private static string CreateSlipMetricsSummary(CharacterLocomotionFootSlipSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Frame == null)
                return "Slip metrics: waiting for character";

            MxAnimationLocomotionCalibrationFrame frame = snapshot.Frame;
            var builder = new StringBuilder();
            builder.Append("Slip metrics: ").Append(snapshot.Grade)
                .Append(" grounded=").Append(snapshot.Grounded ? "true" : "false")
                .Append(" feet=").Append(snapshot.LeftFootResolved ? "L" : "-")
                .Append(snapshot.RightFootResolved ? "R" : "-")
                .Append('\n');
            builder.Append("actual=(").Append(FormatFloat(frame.ActualLocalVelocityX)).Append(',')
                .Append(FormatFloat(frame.ActualLocalVelocityY)).Append(") native=(")
                .Append(FormatFloat(frame.BlendedNativeVelocityX)).Append(',')
                .Append(FormatFloat(frame.BlendedNativeVelocityY)).Append(") error=")
                .Append(FormatFloat(frame.VelocityErrorRatio)).Append('\n');
            builder.Append("contact L=").Append(FormatFloat(frame.LeftFootContactConfidence))
                .Append(" R=").Append(FormatFloat(frame.RightFootContactConfidence))
                .Append(" slip cm/s L=").Append(FormatFloat(frame.LeftFootSlipCmPerSecond))
                .Append(" R=").Append(FormatFloat(frame.RightFootSlipCmPerSecond))
                .Append(" max=").Append(FormatFloat(frame.MaxSlipDistanceCm)).Append('\n');
            if (snapshot.Diagnostics.Count > 0)
            {
                builder.Append("diagnostics:");
                int max = Mathf.Min(snapshot.Diagnostics.Count, 3);
                for (int i = 0; i < max; i++)
                    builder.Append('\n').Append("- ").Append(snapshot.Diagnostics[i]);
            }

            return builder.ToString();
        }

        private void UpdateSceneGizmos()
        {
            if (!_showSceneGizmos || CharacterInstance == null || _footSlipSnapshot == null || _footSlipSnapshot.Frame == null)
            {
                HideSceneGizmos();
                return;
            }

            EnsureSceneGizmoObjects();
            if (_actualVelocityLine == null || _nativeVelocityLine == null)
                return;

            Transform root = CharacterInstance.transform;
            MxAnimationLocomotionCalibrationFrame frame = _footSlipSnapshot.Frame;
            Vector3 origin = root.position + Vector3.up * 0.08f;
            Vector3 actual = root.TransformDirection(new Vector3(frame.ActualLocalVelocityX, 0f, frame.ActualLocalVelocityY));
            Vector3 native = root.TransformDirection(new Vector3(frame.BlendedNativeVelocityX, 0f, frame.BlendedNativeVelocityY));

            SetLine(_actualVelocityLine, origin, origin + actual, new Color(0.1f, 0.9f, 1f, 0.94f), 0.035f);
            SetLine(_nativeVelocityLine, origin + Vector3.up * 0.08f, origin + Vector3.up * 0.08f + native, new Color(1f, 0.82f, 0.15f, 0.94f), 0.028f);

            Color slipColor = GetSlipColor(_footSlipSnapshot.Grade);
            if (_footSlipSnapshot.LeftFootResolved)
            {
                AddTrailPoint(_leftTrail, ref _leftTrailCount, _footSlipSnapshot.LeftFootPosition + Vector3.up * 0.025f);
                UpdateTrailLine(_leftFootTrailLine, _leftTrail, _leftTrailCount, slipColor);
                SetMarker(_leftAnchorMarker, _footSlipSnapshot.LeftFootPlanted, _footSlipSnapshot.LeftFootAnchor, slipColor);
            }
            else
            {
                SetLineVisible(_leftFootTrailLine, false);
                SetMarker(_leftAnchorMarker, false, default, slipColor);
            }

            if (_footSlipSnapshot.RightFootResolved)
            {
                AddTrailPoint(_rightTrail, ref _rightTrailCount, _footSlipSnapshot.RightFootPosition + Vector3.up * 0.025f);
                UpdateTrailLine(_rightFootTrailLine, _rightTrail, _rightTrailCount, slipColor);
                SetMarker(_rightAnchorMarker, _footSlipSnapshot.RightFootPlanted, _footSlipSnapshot.RightFootAnchor, slipColor);
            }
            else
            {
                SetLineVisible(_rightFootTrailLine, false);
                SetMarker(_rightAnchorMarker, false, default, slipColor);
            }
        }

        private void EnsureSceneGizmoObjects()
        {
            if (_gizmoMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
                if (shader != null)
                    _gizmoMaterial = new Material(shader) { name = "MxFramework Locomotion Calibration Gizmo" };
            }

            _actualVelocityLine = EnsureLine(_actualVelocityLine, "LocomotionCalibration_ActualVelocity");
            _nativeVelocityLine = EnsureLine(_nativeVelocityLine, "LocomotionCalibration_AnimationVelocity");
            _leftFootTrailLine = EnsureLine(_leftFootTrailLine, "LocomotionCalibration_LeftFootTrail");
            _rightFootTrailLine = EnsureLine(_rightFootTrailLine, "LocomotionCalibration_RightFootTrail");
            _leftAnchorMarker = EnsureMarker(_leftAnchorMarker, "LocomotionCalibration_LeftFootAnchor");
            _rightAnchorMarker = EnsureMarker(_rightAnchorMarker, "LocomotionCalibration_RightFootAnchor");
        }

        private LineRenderer EnsureLine(LineRenderer line, string name)
        {
            if (line != null)
                return line;

            var go = new GameObject(name)
            {
                hideFlags = HideFlags.DontSave
            };
            go.transform.SetParent(transform, worldPositionStays: false);
            line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.textureMode = LineTextureMode.Stretch;
            line.alignment = LineAlignment.View;
            line.positionCount = 0;
            if (_gizmoMaterial != null)
                line.sharedMaterial = _gizmoMaterial;
            return line;
        }

        private Transform EnsureMarker(Transform marker, string name)
        {
            if (marker != null)
                return marker;

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.hideFlags = HideFlags.DontSave;
            go.transform.SetParent(transform, worldPositionStays: false);
            go.transform.localScale = Vector3.one * 0.09f;
            Collider collider = go.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);
            return go.transform;
        }

        private static void SetLine(LineRenderer line, Vector3 start, Vector3 end, Color color, float width)
        {
            if (line == null)
                return;

            line.gameObject.SetActive(true);
            line.positionCount = 2;
            line.startWidth = width;
            line.endWidth = width * 0.45f;
            line.startColor = color;
            line.endColor = new Color(color.r, color.g, color.b, 0.18f);
            line.SetPosition(0, start);
            line.SetPosition(1, end);
        }

        private static void SetLineVisible(LineRenderer line, bool visible)
        {
            if (line != null)
                line.gameObject.SetActive(visible);
        }

        private static void AddTrailPoint(Vector3[] trail, ref int count, Vector3 point)
        {
            if (count > 0 && (trail[count - 1] - point).sqrMagnitude < 0.0004f)
                return;

            if (count < trail.Length)
            {
                trail[count] = point;
                count++;
                return;
            }

            for (int i = 1; i < trail.Length; i++)
                trail[i - 1] = trail[i];
            trail[trail.Length - 1] = point;
        }

        private static void UpdateTrailLine(LineRenderer line, Vector3[] trail, int count, Color color)
        {
            if (line == null)
                return;

            line.gameObject.SetActive(count > 1);
            line.positionCount = count;
            line.startWidth = 0.018f;
            line.endWidth = 0.018f;
            line.startColor = new Color(color.r, color.g, color.b, 0.18f);
            line.endColor = color;
            for (int i = 0; i < count; i++)
                line.SetPosition(i, trail[i]);
        }

        private static void SetMarker(Transform marker, bool visible, Vector3 position, Color color)
        {
            if (marker == null)
                return;

            marker.gameObject.SetActive(visible);
            if (!visible)
                return;

            marker.position = position + Vector3.up * 0.055f;
            Renderer renderer = marker.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = color;
                renderer.enabled = true;
            }
        }

        private void HideSceneGizmos()
        {
            SetLineVisible(_actualVelocityLine, false);
            SetLineVisible(_nativeVelocityLine, false);
            SetLineVisible(_leftFootTrailLine, false);
            SetLineVisible(_rightFootTrailLine, false);
            if (_leftAnchorMarker != null)
                _leftAnchorMarker.gameObject.SetActive(false);
            if (_rightAnchorMarker != null)
                _rightAnchorMarker.gameObject.SetActive(false);
        }

        private static Color GetSlipColor(MxAnimationFootSlipGrade grade)
        {
            if (grade == MxAnimationFootSlipGrade.Bad)
                return new Color(1f, 0.18f, 0.12f, 0.95f);
            if (grade == MxAnimationFootSlipGrade.Warning)
                return new Color(1f, 0.75f, 0.12f, 0.95f);
            return new Color(0.1f, 0.92f, 0.72f, 0.95f);
        }

        private readonly struct LocomotionPresetDefinition
        {
            public LocomotionPresetDefinition(
                string id,
                string displayName,
                Vector2 direction,
                float startSpeed,
                float endSpeed,
                bool run,
                float durationSeconds)
            {
                Id = id ?? string.Empty;
                DisplayName = displayName ?? string.Empty;
                Direction = direction;
                StartSpeed = Mathf.Clamp01(startSpeed);
                EndSpeed = Mathf.Clamp01(endSpeed);
                Run = run;
                DurationSeconds = Mathf.Max(0.1f, durationSeconds);
            }

            public string Id { get; }
            public string DisplayName { get; }
            public Vector2 Direction { get; }
            public float StartSpeed { get; }
            public float EndSpeed { get; }
            public bool Run { get; }
            public float DurationSeconds { get; }
        }

        private sealed class PresetAccumulator
        {
            private readonly LocomotionPresetDefinition _definition;
            private readonly Dictionary<string, int> _dominantClipCounts = new Dictionary<string, int>();
            private readonly List<string> _diagnostics = new List<string>();
            private float _durationSeconds;
            private float _velocityErrorSum;
            private float _suggestedPlaybackSpeedSum;
            private float _maxSlipDistanceCm;
            private float _maxFootSlipCmPerSecond;
            private int _sampleCount;
            private int _unreachablePointCount;
            private int _resourceErrorCount;
            private int _backendErrorCount;
            private MxAnimationFootSlipGrade _grade = MxAnimationFootSlipGrade.Ok;

            public PresetAccumulator(LocomotionPresetDefinition definition)
            {
                _definition = definition;
            }

            public int SampleCount => _sampleCount;

            public void AddSample(
                CharacterLocomotionFootSlipSnapshot snapshot,
                MxAnimationLocomotionBlendProbeSnapshot probe,
                int resourceErrorCount,
                int backendErrorCount,
                float suggestedPlaybackSpeed)
            {
                if (snapshot == null || snapshot.Frame == null)
                    return;

                MxAnimationLocomotionCalibrationFrame frame = snapshot.Frame;
                _sampleCount++;
                _durationSeconds += Mathf.Max(0f, frame.DeltaTime);
                _velocityErrorSum += frame.VelocityErrorRatio;
                _suggestedPlaybackSpeedSum += Mathf.Max(0f, suggestedPlaybackSpeed);
                _maxSlipDistanceCm = Mathf.Max(_maxSlipDistanceCm, frame.MaxSlipDistanceCm);
                _maxFootSlipCmPerSecond = Mathf.Max(
                    _maxFootSlipCmPerSecond,
                    Mathf.Max(frame.LeftFootSlipCmPerSecond, frame.RightFootSlipCmPerSecond));
                _grade = WorseGrade(_grade, snapshot.Grade);
                _resourceErrorCount = Mathf.Max(_resourceErrorCount, resourceErrorCount);
                _backendErrorCount = Mathf.Max(_backendErrorCount, backendErrorCount);

                if (!string.IsNullOrWhiteSpace(frame.DominantClipId))
                {
                    if (!_dominantClipCounts.ContainsKey(frame.DominantClipId))
                        _dominantClipCounts.Add(frame.DominantClipId, 0);
                    _dominantClipCounts[frame.DominantClipId]++;
                }

                if (probe != null && probe.ReachabilityReport != null)
                {
                    _unreachablePointCount = Mathf.Max(
                        _unreachablePointCount,
                        probe.ReachabilityReport.UnreachablePoints.Count);
                    for (int i = 0; i < probe.ReachabilityReport.Issues.Count; i++)
                        AddDiagnosticOnce(probe.ReachabilityReport.Issues[i].Code + ": " + probe.ReachabilityReport.Issues[i].Message);
                }

                for (int i = 0; i < snapshot.Diagnostics.Count; i++)
                    AddDiagnosticOnce(snapshot.Diagnostics[i]);
            }

            public void AddDiagnostic(string diagnostic)
            {
                AddDiagnosticOnce(diagnostic);
            }

            public MxAnimationLocomotionPresetReport CreateReport()
            {
                var suggestedFields = new List<string>();
                float averageVelocityError = _sampleCount == 0 ? 0f : _velocityErrorSum / _sampleCount;
                float averageSuggestedPlaybackSpeed = _sampleCount == 0 ? 0f : _suggestedPlaybackSpeedSum / _sampleCount;
                if (averageVelocityError > 0.15f)
                {
                    suggestedFields.Add("clip.nativeVelocity");
                    suggestedFields.Add("clip.playbackSpeed");
                }

                if (_maxSlipDistanceCm > MxAnimationFootSlipThresholds.Default.OkMaxSlipDistanceCm
                    || _maxFootSlipCmPerSecond > MxAnimationFootSlipThresholds.Default.OkAverageSlipCmPerSecond)
                {
                    suggestedFields.Add("clip.footContactWindows");
                    suggestedFields.Add("clip.playbackSpeed");
                }

                if (_unreachablePointCount > 0)
                    suggestedFields.Add("blend2d.points");

                if (_sampleCount == 0)
                {
                    _grade = MxAnimationFootSlipGrade.Bad;
                    AddDiagnosticOnce("Preset produced no valid calibration samples.");
                }

                return new MxAnimationLocomotionPresetReport(
                    _definition.Id,
                    _definition.DisplayName,
                    _durationSeconds,
                    _sampleCount,
                    averageVelocityError,
                    _maxSlipDistanceCm,
                    _maxFootSlipCmPerSecond,
                    _grade,
                    GetDominantClipId(),
                    _unreachablePointCount,
                    _resourceErrorCount,
                    _backendErrorCount,
                    averageSuggestedPlaybackSpeed,
                    _diagnostics,
                    suggestedFields);
            }

            private void AddDiagnosticOnce(string diagnostic)
            {
                if (string.IsNullOrWhiteSpace(diagnostic) || _diagnostics.Contains(diagnostic))
                    return;
                _diagnostics.Add(diagnostic);
            }

            private string GetDominantClipId()
            {
                string best = string.Empty;
                int bestCount = 0;
                foreach (KeyValuePair<string, int> pair in _dominantClipCounts)
                {
                    if (pair.Value <= bestCount)
                        continue;
                    best = pair.Key;
                    bestCount = pair.Value;
                }

                return best;
            }

            private static MxAnimationFootSlipGrade WorseGrade(
                MxAnimationFootSlipGrade current,
                MxAnimationFootSlipGrade next)
            {
                if (current == MxAnimationFootSlipGrade.Bad || next == MxAnimationFootSlipGrade.Bad)
                    return MxAnimationFootSlipGrade.Bad;
                if (current == MxAnimationFootSlipGrade.Warning || next == MxAnimationFootSlipGrade.Warning)
                    return MxAnimationFootSlipGrade.Warning;
                return MxAnimationFootSlipGrade.Ok;
            }
        }

        private static void DestroyRuntimeObject(UnityEngine.Object obj)
        {
            if (obj == null)
                return;

            if (Application.isPlaying)
                Destroy(obj);
            else
                DestroyImmediate(obj);
        }

        private static Vector2 MapPoint(
            MxAnimationBlend2DControllerDomain domain,
            int x,
            int y)
        {
            float nx = InverseLerp(domain.MinX, domain.MaxX, x);
            float ny = InverseLerp(domain.MinY, domain.MaxY, y);
            float plotWidth = BlendMapWidth - (BlendMapPadding * 2f);
            float plotHeight = BlendMapHeight - (BlendMapPadding * 2f);
            return new Vector2(
                BlendMapPadding + (Mathf.Clamp01(nx) * plotWidth),
                BlendMapPadding + ((1f - Mathf.Clamp01(ny)) * plotHeight));
        }

        private static float InverseLerp(int min, int max, int value)
        {
            if (min == max)
                return 0.5f;
            return (value - min) / (float)(max - min);
        }

        private static string ShortClipName(ResourceKey key)
        {
            if (!key.IsValid)
                return "-";

            string id = key.Id;
            if (string.IsNullOrWhiteSpace(id))
                return "-";

            int lastDot = id.LastIndexOf('.');
            string name = lastDot >= 0 && lastDot < id.Length - 1 ? id.Substring(lastDot + 1) : id;
            return name.Length <= 10 ? name : name.Substring(0, 10);
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min)
                return min;
            return value > max ? max : value;
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}

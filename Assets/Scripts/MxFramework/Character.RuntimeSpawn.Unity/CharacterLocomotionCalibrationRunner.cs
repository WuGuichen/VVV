using System.Text;
using System.Globalization;
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
        private const float BlendMapWidth = 360f;
        private const float BlendMapHeight = 240f;
        private const float BlendMapPadding = 34f;
        private const float BlendPointWidth = 64f;
        private const float BlendPointHeight = 22f;
        private const float BlendSampleSize = 18f;
        private const float BlendMarkerInset = 6f;

        [SerializeField] private CharacterRuntimeResourceBootstrap _bootstrap;
        [SerializeField] private bool _loadOnStart = true;
        [SerializeField] private bool _keepInputMotionEnabled = true;
        [SerializeField] private bool _showHud = true;
        [SerializeField] private PanelSettings _panelSettings;
        [SerializeField] private int _hudSortOrder = 64;
        [SerializeField] private string _leftFootPath = string.Empty;
        [SerializeField] private string _rightFootPath = string.Empty;
        [SerializeField] private float _footContactThreshold = 0.5f;

        private CharacterRuntimeInputMotionController _motionController;
        private CharacterRuntimeLocomotionBlendController _locomotionController;
        private CharacterLocomotionFootSlipSampler _footSlipSampler;
        private CharacterLocomotionFootSlipSnapshot _footSlipSnapshot;
        private UIDocument _hudDocument;
        private VisualElement _hudRoot;
        private VisualElement _blendMap;
        private Label _hudSummaryLabel;
        private Label _blendWeightsLabel;
        private Label _slipMetricsLabel;

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
            _blendMap = null;
            _hudSummaryLabel = null;
            _blendWeightsLabel = null;
            _slipMetricsLabel = null;
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
            _hudRoot.style.width = 680f;
            _hudRoot.style.maxHeight = 560f;
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

            _blendMap = new VisualElement
            {
                name = "locomotion-calibration-blend-map"
            };
            _blendMap.style.position = Position.Relative;
            _blendMap.style.width = BlendMapWidth;
            _blendMap.style.height = BlendMapHeight;
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

            var help = new Label("WASD: move  |  Shift: run")
            {
                name = "locomotion-calibration-help"
            };
            help.style.marginTop = 8f;
            help.style.fontSize = 12f;
            help.style.color = new Color(0.75f, 0.83f, 0.9f, 1f);

            _hudRoot.Add(title);
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
            _hudRoot.Add(help);
            _hudDocument.rootVisualElement.Add(_hudRoot);
        }

        private void UpdateHud()
        {
            EnsureHud();
            UpdateFootSlipSnapshot();
            if (_hudSummaryLabel != null)
                _hudSummaryLabel.text = CreateHeaderSummary();
            UpdateBlendProbeHud();
            if (_slipMetricsLabel != null)
                _slipMetricsLabel.text = CreateSlipMetricsSummary(_footSlipSnapshot);
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

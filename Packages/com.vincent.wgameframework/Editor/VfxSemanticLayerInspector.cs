using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using MxFramework.Vfx.Unity;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace MxFramework.Editor
{
    internal enum VfxSemanticPropertyCapabilityState
    {
        Confirmed,
        Partial,
        PreviewOnly,
        ReadOnly,
        Unsupported,
        Reserved
    }

    internal sealed class VfxSemanticPropertyCapabilityRow
    {
        public string PropertyId = string.Empty;
        public string DisplayName = string.Empty;
        public string Group = string.Empty;
        public VfxSemanticPropertyCapabilityState State;
        public string Reason = string.Empty;
    }

    internal static class VfxSemanticPropertyCapabilityAnalyzer
    {
        private static readonly HashSet<string> LayerTimingProperties = new HashSet<string>(StringComparer.Ordinal)
        {
            "timing.preRoll.frames",
            "timing.entry.frames",
            "timing.body.frames",
            "timing.exit.frames",
            "timing.cleanup.frames",
            "timing.cycle.frames"
        };

        public static List<VfxSemanticPropertyCapabilityRow> Analyze(
            VfxSemanticPropertyRegistryView registry,
            IReadOnlyList<VfxResolvedComponentAuthoringProfile> members,
            string memberId)
        {
            var result = new List<VfxSemanticPropertyCapabilityRow>();
            if (registry?.Properties == null) return result;
            IReadOnlyList<VfxResolvedComponentAuthoringProfile> targetMembers = string.IsNullOrWhiteSpace(memberId)
                ? members ?? Array.Empty<VfxResolvedComponentAuthoringProfile>()
                : (members ?? Array.Empty<VfxResolvedComponentAuthoringProfile>())
                    .Where(value => string.Equals(value.MemberId, memberId, StringComparison.Ordinal))
                    .ToList();
            for (int propertyIndex = 0; propertyIndex < registry.Properties.Count; propertyIndex++)
            {
                VfxSemanticPropertyDefinitionView property = registry.Properties[propertyIndex];
                var row = new VfxSemanticPropertyCapabilityRow
                {
                    PropertyId = property.PropertyId,
                    DisplayName = property.DisplayName,
                    Group = property.Group
                };
                if (property.PropertyId == "render.material.profile")
                {
                    row.State = VfxSemanticPropertyCapabilityState.Reserved;
                    row.Reason = "预留合同；尚无 Material Variant Profile 数据模型与 Adapter。";
                    result.Add(row);
                    continue;
                }
                if (LayerTimingProperties.Contains(property.PropertyId))
                {
                    int confirmed = targetMembers.Count(value => value.Persisted);
                    row.State = targetMembers.Count == 0 || confirmed == targetMembers.Count
                        ? VfxSemanticPropertyCapabilityState.Confirmed
                        : confirmed == 0 ? VfxSemanticPropertyCapabilityState.PreviewOnly : VfxSemanticPropertyCapabilityState.Partial;
                    row.Reason = targetMembers.Count == 0
                        ? "由 Layer Retime Compiler 处理。"
                        : confirmed == targetMembers.Count
                            ? "由 Layer Retime Compiler 处理；目标成员 Profile 均已确认。"
                            : "使用自动建议绑定；烘焙时会冻结到 Candidate 回执，发布共享规则为可选操作。";
                    result.Add(row);
                    continue;
                }
                int writable = 0;
                int confirmedWritable = 0;
                int readOnly = 0;
                for (int memberIndex = 0; memberIndex < targetMembers.Count; memberIndex++)
                {
                    VfxResolvedComponentAuthoringProfile member = targetMembers[memberIndex];
                    List<VfxSemanticBindingView> bindings = VfxSemanticParticleSystemAdapter.BindingsForProperty(member, property.PropertyId).ToList();
                    if (bindings.Count == 0) continue;
                    if (bindings.All(value => value.ReadOnly))
                    {
                        readOnly++;
                        continue;
                    }
                    writable++;
                    bool needsShader = bindings.Any(value =>
                        value.Adapter == "MaterialPropertyBlock"
                        || value.Adapter == "DerivedMaterial"
                        || value.Adapter == "ParticleCustomDataExact");
                    if (member.Persisted && (!needsShader || member.ShaderProfilePersisted))
                        confirmedWritable++;
                }
                if (targetMembers.Count == 0)
                {
                    row.State = VfxSemanticPropertyCapabilityState.Unsupported;
                    row.Reason = "当前 Layer 没有可分析成员。";
                }
                else if (confirmedWritable == targetMembers.Count)
                {
                    row.State = VfxSemanticPropertyCapabilityState.Confirmed;
                    row.Reason = "所有目标成员均有已确认 Binding 与 Adapter。";
                }
                else if (confirmedWritable > 0)
                {
                    row.State = VfxSemanticPropertyCapabilityState.Partial;
                    row.Reason = confirmedWritable + "/" + targetMembers.Count + " 个目标成员可安全写入；其余成员保持不参与或需确认。";
                }
                else if (writable > 0)
                {
                    row.State = VfxSemanticPropertyCapabilityState.PreviewOnly;
                    row.Reason = "存在自动建议 Binding；可以编辑并烘焙 Candidate，但尚未发布为跨草稿共享规则。";
                }
                else if (readOnly > 0)
                {
                    row.State = VfxSemanticPropertyCapabilityState.ReadOnly;
                    row.Reason = "Profile 只声明只读证据，没有安全写 Adapter。";
                }
                else
                {
                    row.State = VfxSemanticPropertyCapabilityState.Unsupported;
                    row.Reason = "当前目标成员没有该属性 Binding。";
                }
                result.Add(row);
            }
            return result
                .OrderBy(value => value.Group, StringComparer.Ordinal)
                .ThenBy(value => value.DisplayName, StringComparer.Ordinal)
                .ThenBy(value => value.PropertyId, StringComparer.Ordinal)
                .ToList();
        }
    }

    /// <summary>Owns the four-page semantic Layer Inspector without duplicating composition state.</summary>
    /// <remarks>Editor-only, AllocByDesign. All edits are delegated into the composition undo transaction.</remarks>
    internal sealed class VfxSemanticLayerInspectorController : IDisposable
    {
        private static readonly HashSet<string> DerivedTimingPropertyIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "timing.preRoll.frames",
            "timing.entry.frames",
            "timing.body.frames",
            "timing.exit.frames",
            "timing.cleanup.frames",
            "timing.cycle.frames"
        };
        private static readonly string[] RetimeChoices =
        {
            "原速裁切", "伸缩主体", "调整尾迹", "重复主体", "事件重排", "整体变速", "固定源长度"
        };
        private static readonly string[] WindowEndPolicyChoices = { "自然消散", "立即清理", "消散后清理" };
        private static readonly string[] VisualStartChoices = { "自动移除公共空白", "保留源时间", "手动前导" };
        private static readonly string[] ParticipationChoices = { "跟随", "覆盖", "不参与", "不支持", "待确认" };
        private static readonly (string Label, string PropertyId)[] MatrixProperties =
        {
            ("总状态", string.Empty),
            ("可见度", "appearance.opacity"),
            ("主色", "appearance.palette.primary"),
            ("发射", "emission.density"),
            ("速度", "motion.speed"),
            ("尺寸", "shape.particle.size"),
            ("发光", "appearance.emission.exposureEv"),
            ("排序", "render.sortingOrder.offset")
        };
        private readonly VisualElement _root;
        private readonly Func<VfxCompositionLayerView> _selectedLayer;
        private readonly Func<VfxSourceElementCatalog> _catalog;
        private readonly Action<Action<VfxCompositionLayerView>> _changeLayer;
        private readonly Action _recordSharedChange;
        private readonly Action<string, bool> _setStatus;
        private readonly Func<bool> _liveRefresh;
        private readonly VfxSemanticPropertyRegistryView _registry;
        private readonly Button[] _tabs;
        private readonly VisualElement[] _pages;
        private readonly DropdownField _editScope;
        private readonly Label _editScopeSummary;
        private readonly Button _resetSource;
        private readonly DropdownField _retimeMode;
        private readonly IntegerField _startFrame;
        private readonly DropdownField _visualStartMode;
        private readonly IntegerField _manualLeadIn;
        private readonly Label _visualStartSummary;
        private readonly SliderInt _targetFrames;
        private readonly Label _targetFramesError;
        private readonly Label _playbackSourceLength;
        private readonly DropdownField _windowEndPolicy;
        private readonly SliderInt _dissipateFrames;
        private readonly Slider _materialTimeScale;
        private readonly VisualElement _segmentGrid;
        private readonly VisualElement _timeSegmentBar;
        private readonly ProgressBar _preRoll;
        private readonly ProgressBar _entry;
        private readonly ProgressBar _body;
        private readonly SliderInt _minimumBody;
        private readonly ProgressBar _exit;
        private readonly ProgressBar _cleanup;
        private readonly ProgressBar _cycle;
        private readonly Label _minimumLength;
        private readonly VisualElement[] _segmentBars;
        private readonly DropdownField _curveProperty;
        private readonly DropdownField _curveOperation;
        private readonly CurveField _curveEditor;
        private readonly Foldout _minMaxEditor;
        private readonly DropdownField _minMaxMode;
        private readonly FloatField _minMaxMultiplier;
        private readonly FloatField _minMaxConstantMin;
        private readonly FloatField _minMaxConstantMax;
        private readonly CurveField _minMaxCurveMin;
        private readonly CurveField _minMaxCurveMax;
        private readonly Label _curveDomain;
        private readonly IMGUIContainer _curveCanvas;
        private readonly Slider _opacity;
        private readonly ColorField _primaryColor;
        private readonly ColorField _secondaryColor;
        private readonly ColorField _detailColor;
        private readonly ColorField _emissionColor;
        private readonly Slider _emissionEv;
        private readonly Foldout _gradientEditor;
        private readonly DropdownField _gradientMode;
        private readonly ColorField _gradientColorMin;
        private readonly ColorField _gradientColorMax;
        private readonly GradientField _gradientMin;
        private readonly GradientField _gradientMax;
        private readonly Slider _emissionDensity;
        private readonly Slider _burstScale;
        private readonly IntegerField _emissionWindowStart;
        private readonly IntegerField _emissionWindowEnd;
        private readonly IntegerField _capacity;
        private readonly Slider _motionSpeed;
        private readonly FloatField _travelRange;
        private readonly Slider _gravity;
        private readonly Vector3Field _localGravity;
        private readonly Vector3Field _forceVector;
        private readonly Slider _drag;
        private readonly Vector3Field _rotationSpeed;
        private readonly Slider _noiseStrength;
        private readonly Slider _noiseFrequency;
        private readonly Slider _compositionScale;
        private readonly Slider _particleSize;
        private readonly Slider _emitterScale;
        private readonly Slider _shapeRadius;
        private readonly FloatField _shapeAngle;
        private readonly FloatField _shapeArc;
        private readonly Slider _directionSpread;
        private readonly IntegerField _sortingOffset;
        private readonly DropdownField _renderMode;
        private readonly DropdownField _renderAlignment;
        private readonly DropdownField _renderSortMode;
        private readonly Vector3Field _renderPivot;
        private readonly FloatField _renderMinSize;
        private readonly FloatField _renderMaxSize;
        private readonly ObjectField _renderMaterial;
        private readonly ObjectField _renderMesh;
        private readonly ObjectField _textureMain;
        private readonly ObjectField _textureMask;
        private readonly ObjectField _textureNoise;
        private readonly ObjectField _textureRamp;
        private readonly ObjectField _textureNormal;
        private readonly ObjectField _textureFlipbook;
        private readonly Label _textureContract;
        private readonly IntegerField _renderQueue;
        private readonly DropdownField _renderCull;
        private readonly Toggle _renderZWrite;
        private readonly DropdownField _renderSrcBlend;
        private readonly DropdownField _renderDstBlend;
        private readonly Slider _flipbookSpeed;
        private readonly IntegerField _flipbookCycles;
        private readonly Toggle _experimental;
        private readonly DropdownField _memberProperty;
        private readonly ScrollView _memberMatrix;
        private readonly Label _memberSummary;
        private readonly ListView _memberList;
        private readonly Label _profileCoverage;
        private readonly Label _profileImpact;
        private readonly Button _copyProfileGap;
        private readonly Button _confirmProfile;
        private readonly ListView _propertyCapabilityList;
        private readonly Label _propertyCapabilitySummary;
        private readonly ListView _rawList;
        private readonly TextField _rawJson;
        private readonly ListView _diffList;
        private readonly Label _compileHash;
        private readonly Label _compileDiagnostics;
        private readonly List<VfxResolvedComponentAuthoringProfile> _members = new List<VfxResolvedComponentAuthoringProfile>();
        private readonly List<VfxRawFieldRow> _rawRows = new List<VfxRawFieldRow>();
        private readonly List<VfxUnityFieldDiffView> _diffRows = new List<VfxUnityFieldDiffView>();
        private readonly List<VfxSemanticPropertyCapabilityRow> _propertyCapabilityRows = new List<VfxSemanticPropertyCapabilityRow>();
        private readonly List<string> _memberPropertyIds = new List<string>();
        private readonly List<string> _editScopeMemberIds = new List<string>();
        private VfxCompiledLayerView _compiled;
        private VfxResolvedLayerAuthoringProfile _resolved;
        private string _lastCompiledLayerId = string.Empty;
        private string _editMemberId = string.Empty;
        private bool _binding;

        public VfxSemanticLayerInspectorController(
            VisualElement root,
            Func<VfxCompositionLayerView> selectedLayer,
            Func<VfxSourceElementCatalog> catalog,
            Action<Action<VfxCompositionLayerView>> changeLayer,
            Action recordSharedChange,
            Action<string, bool> setStatus,
            Func<bool> liveRefresh)
        {
            _root = root;
            _selectedLayer = selectedLayer;
            _catalog = catalog;
            _changeLayer = changeLayer;
            _recordSharedChange = recordSharedChange;
            _setStatus = setStatus;
            _liveRefresh = liveRefresh;
            _registry = new VfxSemanticPropertyRegistryService().Load(out _);
            _tabs = new[]
            {
                root.Q<Button>("semantic-tab"), root.Q<Button>("semantic-members-tab"),
                root.Q<Button>("semantic-raw-tab"), root.Q<Button>("semantic-diff-tab")
            };
            _pages = new[]
            {
                root.Q("semantic-panel"), root.Q("semantic-members-panel"),
                root.Q("semantic-raw-panel"), root.Q("semantic-diff-panel")
            };
            _editScope = root.Q<DropdownField>("semantic-edit-scope");
            _editScopeSummary = root.Q<Label>("semantic-edit-scope-summary");
            _resetSource = root.Q<Button>("semantic-reset-source");
            _retimeMode = root.Q<DropdownField>("semantic-retime-mode");
            _startFrame = root.Q<IntegerField>("semantic-start-frame");
            _visualStartMode = root.Q<DropdownField>("semantic-visual-start-mode");
            _manualLeadIn = root.Q<IntegerField>("semantic-manual-lead-in");
            _visualStartSummary = root.Q<Label>("semantic-visual-start-summary");
            _targetFrames = root.Q<SliderInt>("semantic-target-frames");
            _targetFramesError = root.Q<Label>("semantic-target-frames-error");
            _playbackSourceLength = root.Q<Label>("semantic-playback-source-length");
            _windowEndPolicy = root.Q<DropdownField>("semantic-window-end-policy");
            _dissipateFrames = root.Q<SliderInt>("semantic-dissipate-frames");
            _materialTimeScale = root.Q<Slider>("semantic-material-time-scale");
            _segmentGrid = root.Q("semantic-segment-grid");
            _timeSegmentBar = root.Q("semantic-time-segment-bar");
            _preRoll = root.Q<ProgressBar>("semantic-preroll-frames");
            _entry = root.Q<ProgressBar>("semantic-entry-frames");
            _body = root.Q<ProgressBar>("semantic-body-frames");
            _minimumBody = root.Q<SliderInt>("semantic-minimum-body-frames");
            _exit = root.Q<ProgressBar>("semantic-exit-frames");
            _cleanup = root.Q<ProgressBar>("semantic-cleanup-frames");
            _cycle = root.Q<ProgressBar>("semantic-cycle-frames");
            _minimumLength = root.Q<Label>("semantic-minimum-length");
            _segmentBars = new[]
            {
                root.Q("semantic-segment-preroll"), root.Q("semantic-segment-entry"), root.Q("semantic-segment-body"),
                root.Q("semantic-segment-exit"), root.Q("semantic-segment-cleanup")
            };
            _curveProperty = root.Q<DropdownField>("semantic-curve-property");
            _curveOperation = root.Q<DropdownField>("semantic-curve-operation");
            _curveEditor = root.Q<CurveField>("semantic-curve-editor");
            _minMaxEditor = root.Q<Foldout>("semantic-minmax-editor");
            _minMaxMode = root.Q<DropdownField>("semantic-minmax-mode");
            _minMaxMultiplier = root.Q<FloatField>("semantic-minmax-multiplier");
            _minMaxConstantMin = root.Q<FloatField>("semantic-minmax-constant-min");
            _minMaxConstantMax = root.Q<FloatField>("semantic-minmax-constant-max");
            _minMaxCurveMin = root.Q<CurveField>("semantic-minmax-curve-min");
            _minMaxCurveMax = root.Q<CurveField>("semantic-minmax-curve-max");
            _curveDomain = root.Q<Label>("semantic-curve-domain");
            VisualElement curveHost = root.Q("semantic-curve-track");
            _curveCanvas = new IMGUIContainer(DrawCurveTrack) { name = "semantic-curve-canvas" };
            _curveCanvas.style.flexGrow = 1f;
            curveHost.Add(_curveCanvas);
            _opacity = root.Q<Slider>("semantic-opacity");
            _primaryColor = root.Q<ColorField>("semantic-primary-color");
            _secondaryColor = root.Q<ColorField>("semantic-secondary-color");
            _detailColor = root.Q<ColorField>("semantic-detail-color");
            _emissionColor = root.Q<ColorField>("semantic-emission-color");
            _emissionEv = root.Q<Slider>("semantic-emission-ev");
            _gradientEditor = root.Q<Foldout>("semantic-gradient-editor");
            _gradientMode = root.Q<DropdownField>("semantic-gradient-mode");
            _gradientColorMin = root.Q<ColorField>("semantic-gradient-color-min");
            _gradientColorMax = root.Q<ColorField>("semantic-gradient-color-max");
            _gradientMin = root.Q<GradientField>("semantic-gradient-min");
            _gradientMax = root.Q<GradientField>("semantic-gradient-max");
            _emissionDensity = root.Q<Slider>("semantic-emission-density");
            _burstScale = root.Q<Slider>("semantic-burst-scale");
            _emissionWindowStart = root.Q<IntegerField>("semantic-emission-window-start");
            _emissionWindowEnd = root.Q<IntegerField>("semantic-emission-window-end");
            _capacity = root.Q<IntegerField>("semantic-capacity");
            _motionSpeed = root.Q<Slider>("semantic-motion-speed");
            _travelRange = root.Q<FloatField>("semantic-travel-range");
            _gravity = root.Q<Slider>("semantic-gravity");
            _localGravity = root.Q<Vector3Field>("semantic-local-gravity");
            _forceVector = root.Q<Vector3Field>("semantic-force-vector");
            _drag = root.Q<Slider>("semantic-drag");
            _rotationSpeed = root.Q<Vector3Field>("semantic-rotation-speed");
            _noiseStrength = root.Q<Slider>("semantic-noise-strength");
            _noiseFrequency = root.Q<Slider>("semantic-noise-frequency");
            _compositionScale = root.Q<Slider>("semantic-composition-scale");
            _particleSize = root.Q<Slider>("semantic-particle-size");
            _emitterScale = root.Q<Slider>("semantic-emitter-scale");
            _shapeRadius = root.Q<Slider>("semantic-shape-radius");
            _shapeAngle = root.Q<FloatField>("semantic-shape-angle");
            _shapeArc = root.Q<FloatField>("semantic-shape-arc");
            _directionSpread = root.Q<Slider>("semantic-direction-spread");
            _sortingOffset = root.Q<IntegerField>("semantic-sorting-offset");
            _renderMode = root.Q<DropdownField>("semantic-render-mode");
            _renderAlignment = root.Q<DropdownField>("semantic-render-alignment");
            _renderSortMode = root.Q<DropdownField>("semantic-render-sort-mode");
            _renderPivot = root.Q<Vector3Field>("semantic-render-pivot");
            _renderMinSize = root.Q<FloatField>("semantic-render-min-size");
            _renderMaxSize = root.Q<FloatField>("semantic-render-max-size");
            _renderMaterial = root.Q<ObjectField>("semantic-render-material");
            _renderMesh = root.Q<ObjectField>("semantic-render-mesh");
            _textureMain = root.Q<ObjectField>("semantic-texture-main");
            _textureMask = root.Q<ObjectField>("semantic-texture-mask");
            _textureNoise = root.Q<ObjectField>("semantic-texture-noise");
            _textureRamp = root.Q<ObjectField>("semantic-texture-ramp");
            _textureNormal = root.Q<ObjectField>("semantic-texture-normal");
            _textureFlipbook = root.Q<ObjectField>("semantic-texture-flipbook");
            _textureContract = root.Q<Label>("semantic-texture-contract");
            _renderQueue = root.Q<IntegerField>("semantic-render-queue");
            _renderCull = root.Q<DropdownField>("semantic-render-cull");
            _renderZWrite = root.Q<Toggle>("semantic-render-zwrite");
            _renderSrcBlend = root.Q<DropdownField>("semantic-render-src-blend");
            _renderDstBlend = root.Q<DropdownField>("semantic-render-dst-blend");
            _flipbookSpeed = root.Q<Slider>("semantic-flipbook-speed");
            _flipbookCycles = root.Q<IntegerField>("semantic-flipbook-cycles");
            _experimental = root.Q<Toggle>("semantic-experimental");
            _memberProperty = root.Q<DropdownField>("semantic-member-property");
            _memberMatrix = root.Q<ScrollView>("semantic-member-matrix");
            _memberSummary = root.Q<Label>("semantic-member-summary");
            _memberList = root.Q<ListView>("semantic-member-list");
            _profileCoverage = root.Q<Label>("semantic-profile-coverage");
            _profileImpact = root.Q<Label>("semantic-profile-impact");
            _copyProfileGap = root.Q<Button>("semantic-copy-profile-gap");
            _confirmProfile = root.Q<Button>("semantic-confirm-profile");
            _propertyCapabilityList = root.Q<ListView>("semantic-property-capability-list");
            _propertyCapabilitySummary = root.Q<Label>("semantic-property-capability-summary");
            _rawList = root.Q<ListView>("semantic-raw-list");
            _rawJson = root.Q<TextField>("semantic-raw-json");
            _diffList = root.Q<ListView>("semantic-diff-list");
            _compileHash = root.Q<Label>("semantic-compile-hash");
            _compileDiagnostics = root.Q<Label>("semantic-compile-diagnostics");
            ConfigureLists();
            ConfigurePropertyCapabilityList();
            ConfigureCallbacks();
            ConfigureMemberProperties();
            _memberMatrix.mode = ScrollViewMode.Vertical;
            _memberMatrix.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            RefreshEditScopeChoices();
            ShowPage(0);
        }

        public void Bind(VfxCompositionLayerView layer)
        {
            _binding = true;
            try
            {
                bool available = layer != null;
                for (int i = 0; i < _pages.Length; i++) _pages[i].SetEnabled(available);
                if (!available)
                {
                    SetTargetFrameValidation(string.Empty);
                    _members.Clear();
                    _memberMatrix?.Clear();
                    _rawRows.Clear();
                    _diffRows.Clear();
                    _memberList.Rebuild();
                    _rawList.Rebuild();
                    _diffList.Rebuild();
                    return;
                }
                layer.Timing ??= new VfxLayerTimingView();
                VfxTimeSegmentsView source = layer.Timing.SourceSegments ?? new VfxTimeSegmentsView();
                ConfigureTimingSliderRanges(layer, source);
                _retimeMode.SetValueWithoutNotify(RetimeModeLabel(layer.Timing.RetimeMode));
                _startFrame.SetValueWithoutNotify(layer.StartFrame);
                VfxVisualStartMode visualStartMode = ResolveVisualStartMode(layer);
                _visualStartMode.SetValueWithoutNotify(VisualStartLabel(visualStartMode));
                _manualLeadIn.SetValueWithoutNotify(Math.Max(0, layer.VisualStart?.ManualLeadInFrames ?? 0));
                _manualLeadIn.SetEnabled(visualStartMode == VfxVisualStartMode.ManualLeadIn);
                _targetFrames.SetValueWithoutNotify(layer.Timing.TargetArtisticFrames > 0 ? layer.Timing.TargetArtisticFrames : source.ArtisticFrames);
                _experimental.SetValueWithoutNotify(layer.Timing.ExperimentalAcknowledged);
                SetSemanticFields(layer);
            }
            finally { _binding = false; }
            if ((_liveRefresh?.Invoke() ?? true) || !string.Equals(_lastCompiledLayerId, layer?.LayerId, StringComparison.Ordinal))
            {
                CompileSelectedLayer();
                _lastCompiledLayerId = layer?.LayerId ?? string.Empty;
            }
            else
            {
                _compileDiagnostics.text = "实时刷新已关闭；语义值已记录，曲线与字段差异将在重新开启后更新。";
                _compileDiagnostics.EnableInClassList("warning", true);
            }
        }

        public void RefreshNow()
        {
            CompileSelectedLayer();
            _lastCompiledLayerId = _selectedLayer()?.LayerId ?? string.Empty;
        }

        public void Dispose()
        {
            _curveCanvas.onGUIHandler = null;
            _members.Clear();
            _rawRows.Clear();
            _diffRows.Clear();
        }

        private void ConfigurePropertyCapabilityList()
        {
            _propertyCapabilityList.itemsSource = _propertyCapabilityRows;
            _propertyCapabilityList.fixedItemHeight = 44;
            _propertyCapabilityList.virtualizationMethod = CollectionVirtualizationMethod.FixedHeight;
            _propertyCapabilityList.selectionType = SelectionType.None;
            _propertyCapabilityList.makeItem = () =>
            {
                var row = new VisualElement();
                row.AddToClassList("semantic-capability-row");
                var top = new VisualElement();
                top.AddToClassList("semantic-capability-row-top");
                var name = new Label { name = "capability-name" };
                name.AddToClassList("semantic-capability-name");
                var state = new Label { name = "capability-state" };
                state.AddToClassList("semantic-capability-state");
                top.Add(name);
                top.Add(state);
                row.Add(top);
                var reason = new Label { name = "capability-reason" };
                reason.AddToClassList("semantic-capability-reason");
                row.Add(reason);
                return row;
            };
            _propertyCapabilityList.bindItem = (element, index) =>
            {
                VfxSemanticPropertyCapabilityRow row = _propertyCapabilityRows[index];
                element.Q<Label>("capability-name").text = row.DisplayName + " · " + row.PropertyId;
                Label state = element.Q<Label>("capability-state");
                state.text = CapabilityLabel(row.State);
                state.ClearClassList();
                state.AddToClassList("semantic-capability-state");
                state.AddToClassList("state-" + row.State.ToString().ToLowerInvariant());
                element.Q<Label>("capability-reason").text = row.Group + " · " + row.Reason;
            };
        }

        private static string CapabilityLabel(VfxSemanticPropertyCapabilityState state)
        {
            return state switch
            {
                VfxSemanticPropertyCapabilityState.Confirmed => "可安全写入",
                VfxSemanticPropertyCapabilityState.Partial => "部分成员",
                VfxSemanticPropertyCapabilityState.PreviewOnly => "候选绑定",
                VfxSemanticPropertyCapabilityState.ReadOnly => "只读",
                VfxSemanticPropertyCapabilityState.Reserved => "预留",
                _ => "不支持"
            };
        }

        private void ConfigureLists()
        {
            _memberList.itemsSource = _members;
            _memberList.fixedItemHeight = 72;
            _memberList.makeItem = MakeMemberRow;
            _memberList.bindItem = BindMemberRow;
            _memberList.selectionChanged += selection =>
            {
                VfxResolvedComponentAuthoringProfile selected = selection.OfType<VfxResolvedComponentAuthoringProfile>().FirstOrDefault();
                _rawJson.SetValueWithoutNotify(selected?.Snapshot == null ? string.Empty : JsonConvert.SerializeObject(selected.Snapshot, Formatting.Indented));
            };
            _rawList.itemsSource = _rawRows;
            _rawList.fixedItemHeight = 46;
            _rawList.makeItem = MakeRawRow;
            _rawList.bindItem = BindRawRow;
            _diffList.itemsSource = _diffRows;
            _diffList.fixedItemHeight = 54;
            _diffList.makeItem = MakeDiffRow;
            _diffList.bindItem = BindDiffRow;
        }

        private void ConfigureCallbacks()
        {
            for (int i = 0; i < _tabs.Length; i++)
            {
                int index = i;
                _tabs[i].clicked += () => ShowPage(index);
            }
            _retimeMode.choices = RetimeChoices.ToList();
            _retimeMode.SetValueWithoutNotify(RetimeChoices[0]);
            _windowEndPolicy.choices = WindowEndPolicyChoices.ToList();
            _windowEndPolicy.SetValueWithoutNotify(WindowEndPolicyChoices[0]);
            _visualStartMode.choices = VisualStartChoices.ToList();
            _visualStartMode.SetValueWithoutNotify(VisualStartChoices[0]);
            _curveProperty.choices = new List<string>
            {
                "透明度包络", "发射包络", "距离发射包络", "粒子尺寸", "运动速度", "Noise 强度", "溶解", "侵蚀", "材质局部时间",
                "起始延迟（精确）", "粒子生命周期（精确）", "初始速度（精确）", "初始尺寸（精确）",
                "初始尺寸 X（精确）", "初始尺寸 Y（精确）", "初始尺寸 Z（精确）", "初始旋转（精确）",
                "初始旋转 X（精确）", "初始旋转 Y（精确）", "初始旋转 Z（精确）",
                "持续受力 X（精确）", "持续受力 Y（精确）", "持续受力 Z（精确）",
                "旋转速度 X（精确）", "旋转速度 Y（精确）", "旋转速度 Z（精确）",
                "Noise 滚动（精确）", "Flipbook 帧（精确）", "Flipbook 起始帧（精确）",
                "Custom1 X（精确）", "Custom1 Y（精确）", "Custom1 Z（精确）", "Custom1 W（精确）",
                "Custom2 X（精确）", "Custom2 Y（精确）", "Custom2 Z（精确）", "Custom2 W（精确）"
            };
            _curveProperty.SetValueWithoutNotify(_curveProperty.choices[0]);
            _curveProperty.RegisterValueChangedCallback(_ =>
            {
                _curveDomain.text = ResolveCurveDomain(_curveProperty.value);
                BindCurveEditor(_selectedLayer());
                BindMinMaxEditor(_selectedLayer(), loadSourceWhenMissing: false);
                ApplyCurveCapability();
                _curveCanvas.MarkDirtyRepaint();
            });
            _editScope.RegisterValueChangedCallback(evt =>
            {
                if (_binding) return;
                int index = _editScope.choices.IndexOf(evt.newValue);
                _editMemberId = index >= 0 && index < _editScopeMemberIds.Count
                    ? _editScopeMemberIds[index]
                    : string.Empty;
                Bind(_selectedLayer());
            });
            _curveOperation.choices = new List<string> { nameof(VfxSemanticOperation.Multiply), nameof(VfxSemanticOperation.Add), nameof(VfxSemanticOperation.Remap), nameof(VfxSemanticOperation.Replace) };
            _curveOperation.RegisterValueChangedCallback(evt =>
            {
                if (_binding || IsExactMinMaxOnlyProperty(CurvePropertyId(_curveProperty.value))) return;
                Change(layer => UpsertCurve(layer, CurvePropertyId(_curveProperty.value), ParseOperation(evt.newValue), _curveEditor.value));
            });
            _curveEditor.RegisterValueChangedCallback(evt =>
            {
                if (_binding || IsExactMinMaxOnlyProperty(CurvePropertyId(_curveProperty.value))) return;
                Change(layer => UpsertCurve(layer, CurvePropertyId(_curveProperty.value), ParseOperation(_curveOperation.value), evt.newValue));
            });
            _root.Q<Button>("semantic-curve-reset").clicked += () => Change(layer => Remove(layer, CurvePropertyId(_curveProperty.value)));
            _minMaxMode.choices = new List<string> { nameof(ParticleSystemCurveMode.Constant), nameof(ParticleSystemCurveMode.TwoConstants), nameof(ParticleSystemCurveMode.Curve), nameof(ParticleSystemCurveMode.TwoCurves) };
            _minMaxMode.RegisterValueChangedCallback(_ => UpdateMinMaxModeVisibility());
            _root.Q<Button>("semantic-minmax-use-source").clicked += () => BindMinMaxEditor(_selectedLayer(), loadSourceWhenMissing: true);
            _root.Q<Button>("semantic-minmax-apply").clicked += ApplyExactMinMaxCurve;
            _root.Q<Button>("semantic-minmax-clear").clicked += () => Change(layer => Remove(layer, CurvePropertyId(_curveProperty.value)));
            _gradientMode.choices = new List<string>
            {
                nameof(ParticleSystemGradientMode.Color),
                nameof(ParticleSystemGradientMode.TwoColors),
                nameof(ParticleSystemGradientMode.Gradient),
                nameof(ParticleSystemGradientMode.TwoGradients),
                nameof(ParticleSystemGradientMode.RandomColor)
            };
            _gradientMode.RegisterValueChangedCallback(_ => UpdateGradientModeVisibility());
            _root.Q<Button>("semantic-gradient-use-source").clicked += () => BindGradientEditor(_selectedLayer(), loadSource: true);
            _root.Q<Button>("semantic-gradient-apply").clicked += ApplyExactGradient;
            _root.Q<Button>("semantic-gradient-clear").clicked += () => Change(layer => Remove(layer, "appearance.colorOverLifetime.exact"));
            _retimeMode.RegisterValueChangedCallback(evt => Change(layer =>
            {
                layer.Timing.RetimeMode = RetimeMode(evt.newValue).ToString();
                ClearDerivedTimingOverrides(layer);
            }));
            _startFrame.RegisterValueChangedCallback(evt => Change(layer => layer.StartFrame = Math.Max(0, evt.newValue)));
            _visualStartMode.RegisterValueChangedCallback(evt => Change(layer =>
            {
                layer.VisualStart ??= new VfxLayerVisualStartView();
                layer.VisualStart.Mode = VisualStartMode(evt.newValue).ToString();
            }));
            _manualLeadIn.RegisterValueChangedCallback(evt => Change(layer =>
            {
                layer.VisualStart ??= new VfxLayerVisualStartView();
                layer.VisualStart.Mode = nameof(VfxVisualStartMode.ManualLeadIn);
                layer.VisualStart.ManualLeadInFrames = Math.Max(0, evt.newValue);
            }));
            _targetFrames.RegisterValueChangedCallback(evt => Change(layer =>
            {
                layer.Timing.TargetArtisticFrames = Math.Max(1, evt.newValue);
                ClearDerivedTimingOverrides(layer);
            }));
            _windowEndPolicy.RegisterValueChangedCallback(evt => Change(layer =>
            {
                layer.Timing.WindowEndPolicy = WindowEndPolicy(evt.newValue).ToString();
                if (layer.Timing.WindowEndPolicy != nameof(UnityVfxLayerWindowEndPolicy.DissipateThenClear))
                    layer.Timing.DissipateFrames = 0;
            }));
            _dissipateFrames.RegisterValueChangedCallback(evt => Change(layer =>
                layer.Timing.DissipateFrames = Math.Max(0, evt.newValue)));
            RegisterFloat(_materialTimeScale, "timing.materialLocalTime.scale", VfxSemanticOperation.Multiply, 1f);
            _minimumBody.RegisterValueChangedCallback(evt => Change(layer =>
            {
                layer.Timing.MinimumBodyFrames = Math.Max(1, evt.newValue);
                ClearDerivedTimingOverrides(layer);
            }));
            RegisterFloat(_opacity, "appearance.opacity", VfxSemanticOperation.Multiply, 1f);
            RegisterColor(_primaryColor, "appearance.palette.primary");
            RegisterColor(_secondaryColor, "appearance.palette.secondary");
            RegisterColor(_detailColor, "appearance.palette.detail");
            RegisterColor(_emissionColor, "appearance.emission.color");
            RegisterFloat(_emissionEv, "appearance.emission.exposureEv", VfxSemanticOperation.Add, 0f);
            RegisterFloat(_emissionDensity, "emission.density", VfxSemanticOperation.Multiply, 1f);
            RegisterFloat(_burstScale, "emission.burst.scale", VfxSemanticOperation.Multiply, 1f);
            _emissionWindowStart.RegisterValueChangedCallback(evt => Change(layer => UpsertInt(layer, "emission.window.startFrame", VfxSemanticOperation.Replace, Math.Max(0, evt.newValue), -1)));
            _emissionWindowEnd.RegisterValueChangedCallback(evt => Change(layer => UpsertInt(layer, "emission.window.endFrame", VfxSemanticOperation.Replace, Math.Max(1, evt.newValue), -1)));
            _capacity.RegisterValueChangedCallback(evt => Change(layer => UpsertInt(layer, "emission.capacity.maxParticles", VfxSemanticOperation.Replace, Math.Max(0, evt.newValue), 0)));
            RegisterFloat(_motionSpeed, "motion.speed", VfxSemanticOperation.Multiply, 1f);
            _travelRange.RegisterValueChangedCallback(evt => Change(layer => UpsertFloat(layer, "motion.travelRange", VfxSemanticOperation.Replace, Mathf.Max(0f, evt.newValue), 0f)));
            RegisterFloat(_gravity, "motion.gravity.world", VfxSemanticOperation.Multiply, 1f);
            RegisterVector(_localGravity, "motion.gravity.local", VfxSemanticOperation.Add);
            RegisterVector(_forceVector, "motion.force.vector", VfxSemanticOperation.Add);
            RegisterFloat(_drag, "motion.drag", VfxSemanticOperation.Add, 0f);
            RegisterVector(_rotationSpeed, "motion.rotation.speed", VfxSemanticOperation.Replace);
            RegisterFloat(_noiseStrength, "motion.noise.strength", VfxSemanticOperation.Multiply, 1f);
            RegisterFloat(_noiseFrequency, "motion.noise.frequency", VfxSemanticOperation.Multiply, 1f);
            RegisterFloat(_compositionScale, "shape.composition.scale", VfxSemanticOperation.Multiply, 1f);
            RegisterFloat(_particleSize, "shape.particle.size", VfxSemanticOperation.Multiply, 1f);
            RegisterFloat(_emitterScale, "shape.emitter.scale", VfxSemanticOperation.Multiply, 1f);
            RegisterFloat(_shapeRadius, "shape.radius", VfxSemanticOperation.Multiply, 1f);
            RegisterFloat(_shapeAngle, "shape.angle", VfxSemanticOperation.Add, 0f);
            RegisterFloat(_shapeArc, "shape.arc", VfxSemanticOperation.Add, 0f);
            RegisterFloat(_directionSpread, "shape.direction.spread", VfxSemanticOperation.Add, 0f);
            _sortingOffset.RegisterValueChangedCallback(evt => Change(layer => UpsertInt(layer, "render.sortingOrder.offset", VfxSemanticOperation.Add, evt.newValue, 0)));
            _renderMode.choices = Enum.GetNames(typeof(ParticleSystemRenderMode)).ToList();
            _renderAlignment.choices = Enum.GetNames(typeof(ParticleSystemRenderSpace)).ToList();
            _renderSortMode.choices = Enum.GetNames(typeof(ParticleSystemSortMode)).ToList();
            _renderMode.RegisterValueChangedCallback(evt => Change(layer => UpsertInt(layer, "render.renderer.mode", VfxSemanticOperation.Replace, (int)Enum.Parse<ParticleSystemRenderMode>(evt.newValue), int.MinValue)));
            _renderAlignment.RegisterValueChangedCallback(evt => Change(layer => UpsertInt(layer, "render.renderer.alignment", VfxSemanticOperation.Replace, (int)Enum.Parse<ParticleSystemRenderSpace>(evt.newValue), int.MinValue)));
            _renderSortMode.RegisterValueChangedCallback(evt => Change(layer => UpsertInt(layer, "render.renderer.sortMode", VfxSemanticOperation.Replace, (int)Enum.Parse<ParticleSystemSortMode>(evt.newValue), int.MinValue)));
            _renderPivot.RegisterValueChangedCallback(evt => Change(layer => UpsertVectorExact(layer, "render.renderer.pivot", evt.newValue)));
            RegisterFloat(_renderMinSize, "render.renderer.minParticleSize", VfxSemanticOperation.Replace, -1f);
            RegisterFloat(_renderMaxSize, "render.renderer.maxParticleSize", VfxSemanticOperation.Replace, -1f);
            _renderMaterial.objectType = typeof(Material);
            _renderMesh.objectType = typeof(Mesh);
            _renderMaterial.RegisterValueChangedCallback(evt => Change(layer => UpsertAsset(layer, "render.renderer.material", evt.newValue)));
            _renderMesh.RegisterValueChangedCallback(evt => Change(layer => UpsertAsset(layer, "render.renderer.mesh", evt.newValue)));
            ConfigureTextureField(_textureMain, "render.texture.main");
            ConfigureTextureField(_textureMask, "render.texture.mask");
            ConfigureTextureField(_textureNoise, "render.texture.noise");
            ConfigureTextureField(_textureRamp, "render.texture.ramp");
            ConfigureTextureField(_textureNormal, "render.texture.normal");
            ConfigureTextureField(_textureFlipbook, "render.texture.flipbook");
            _renderQueue.RegisterValueChangedCallback(evt => Change(layer => UpsertInt(layer, "render.state.renderQueue", VfxSemanticOperation.Replace, Mathf.Clamp(evt.newValue, 0, 5000), 0)));
            _renderCull.choices = Enum.GetNames(typeof(CullMode)).ToList();
            _renderSrcBlend.choices = Enum.GetNames(typeof(BlendMode)).ToList();
            _renderDstBlend.choices = Enum.GetNames(typeof(BlendMode)).ToList();
            _renderCull.RegisterValueChangedCallback(evt => Change(layer => UpsertInt(layer, "render.state.cull", VfxSemanticOperation.Replace, (int)Enum.Parse<CullMode>(evt.newValue), int.MinValue)));
            _renderZWrite.RegisterValueChangedCallback(evt => Change(layer => UpsertBool(layer, "render.state.zWrite", evt.newValue)));
            _renderSrcBlend.RegisterValueChangedCallback(evt => Change(layer => UpsertInt(layer, "render.state.srcBlend", VfxSemanticOperation.Replace, (int)Enum.Parse<BlendMode>(evt.newValue), int.MinValue)));
            _renderDstBlend.RegisterValueChangedCallback(evt => Change(layer => UpsertInt(layer, "render.state.dstBlend", VfxSemanticOperation.Replace, (int)Enum.Parse<BlendMode>(evt.newValue), int.MinValue)));
            RegisterFloat(_flipbookSpeed, "render.flipbook.speed", VfxSemanticOperation.Multiply, 1f);
            _flipbookCycles.RegisterValueChangedCallback(evt => Change(layer => UpsertInt(layer, "render.flipbook.cycles", VfxSemanticOperation.Replace, Math.Max(0, evt.newValue), 0)));
            _experimental.RegisterValueChangedCallback(evt => Change(layer => layer.Timing.ExperimentalAcknowledged = evt.newValue));
            _resetSource.clicked += ResetToSource;
            _copyProfileGap.clicked += CopyProfileGapReport;
            _confirmProfile.clicked += ConfirmSharedProfile;
            foreach (Slider slider in _root.Query<Slider>(className: "studio-field").ToList())
                slider.showInputField = true;
        }

        private void ConfigureMemberProperties()
        {
            var choices = new List<string> { "全部属性（成员总状态）" };
            _memberPropertyIds.Clear();
            _memberPropertyIds.Add(string.Empty);
            if (_registry != null)
            {
                foreach (VfxSemanticPropertyDefinitionView definition in _registry.Properties
                    .OrderBy(value => value.Group, StringComparer.Ordinal)
                    .ThenBy(value => value.DisplayName, StringComparer.Ordinal))
                {
                    choices.Add(definition.Group + " / " + definition.DisplayName);
                    _memberPropertyIds.Add(definition.PropertyId);
                }
            }
            _memberProperty.choices = choices;
            _memberProperty.SetValueWithoutNotify(choices[0]);
            _memberProperty.RegisterValueChangedCallback(_ => _memberList.Rebuild());
        }

        private void RegisterFloat(Slider field, string propertyId, VfxSemanticOperation operation, float neutral)
        {
            field.RegisterValueChangedCallback(evt => Change(layer => UpsertFloat(layer, propertyId, operation, evt.newValue, neutral)));
        }

        private void RegisterFloat(FloatField field, string propertyId, VfxSemanticOperation operation, float neutral)
        {
            field.RegisterValueChangedCallback(evt => Change(layer => UpsertFloat(layer, propertyId, operation, evt.newValue, neutral)));
        }

        private void RegisterColor(ColorField field, string propertyId)
        {
            field.RegisterValueChangedCallback(evt => Change(layer => UpsertColor(layer, propertyId, evt.newValue)));
        }

        private void RegisterVector(Vector3Field field, string propertyId, VfxSemanticOperation operation)
        {
            field.RegisterValueChangedCallback(evt => Change(layer => UpsertVector(layer, propertyId, operation, evt.newValue)));
        }

        private void Change(Action<VfxCompositionLayerView> mutation)
        {
            if (_binding) return;
            _changeLayer(mutation);
            Bind(_selectedLayer());
        }

        private void SetSemanticFields(VfxCompositionLayerView layer)
        {
            UpdateEditScopeSummary(layer);
            BindCurveEditor(layer);
            BindMinMaxEditor(layer, loadSourceWhenMissing: false);
            BindGradientEditor(layer, loadSource: false);
            _materialTimeScale.SetValueWithoutNotify(Float(layer, "timing.materialLocalTime.scale", 1f));
            _opacity.SetValueWithoutNotify(Float(layer, "appearance.opacity", 1f));
            _primaryColor.SetValueWithoutNotify(Color(layer, "appearance.palette.primary", UnityEngine.Color.white));
            _secondaryColor.SetValueWithoutNotify(Color(layer, "appearance.palette.secondary", UnityEngine.Color.white));
            _detailColor.SetValueWithoutNotify(Color(layer, "appearance.palette.detail", UnityEngine.Color.white));
            _emissionColor.SetValueWithoutNotify(Color(layer, "appearance.emission.color", UnityEngine.Color.white));
            _emissionEv.SetValueWithoutNotify(Float(layer, "appearance.emission.exposureEv", 0f));
            _emissionDensity.SetValueWithoutNotify(Float(layer, "emission.density", 1f));
            _burstScale.SetValueWithoutNotify(Float(layer, "emission.burst.scale", 1f));
            _emissionWindowStart.SetValueWithoutNotify(Int(layer, "emission.window.startFrame", 0));
            _emissionWindowEnd.SetValueWithoutNotify(Int(layer, "emission.window.endFrame", 0));
            _capacity.SetValueWithoutNotify(Int(layer, "emission.capacity.maxParticles", 0));
            _motionSpeed.SetValueWithoutNotify(Float(layer, "motion.speed", 1f));
            _travelRange.SetValueWithoutNotify(Float(layer, "motion.travelRange", 0f));
            _gravity.SetValueWithoutNotify(Float(layer, "motion.gravity.world", 1f));
            _localGravity.SetValueWithoutNotify(Vector(layer, "motion.gravity.local", Vector3.zero));
            _forceVector.SetValueWithoutNotify(Vector(layer, "motion.force.vector", Vector3.zero));
            _drag.SetValueWithoutNotify(Float(layer, "motion.drag", 0f));
            _rotationSpeed.SetValueWithoutNotify(Vector(layer, "motion.rotation.speed", Vector3.zero));
            _noiseStrength.SetValueWithoutNotify(Float(layer, "motion.noise.strength", 1f));
            _noiseFrequency.SetValueWithoutNotify(Float(layer, "motion.noise.frequency", 1f));
            _compositionScale.SetValueWithoutNotify(Float(layer, "shape.composition.scale", 1f));
            _particleSize.SetValueWithoutNotify(Float(layer, "shape.particle.size", 1f));
            _emitterScale.SetValueWithoutNotify(Float(layer, "shape.emitter.scale", 1f));
            _shapeRadius.SetValueWithoutNotify(Float(layer, "shape.radius", 1f));
            _shapeAngle.SetValueWithoutNotify(Float(layer, "shape.angle", 0f));
            _shapeArc.SetValueWithoutNotify(Float(layer, "shape.arc", 0f));
            _directionSpread.SetValueWithoutNotify(Float(layer, "shape.direction.spread", 0f));
            _sortingOffset.SetValueWithoutNotify(Int(layer, "render.sortingOrder.offset", 0));
            SetRendererFields(layer);
            _flipbookSpeed.SetValueWithoutNotify(Float(layer, "render.flipbook.speed", 1f));
            _flipbookCycles.SetValueWithoutNotify(Int(layer, "render.flipbook.cycles", 0));
        }

        private void CompileSelectedLayer()
        {
            VfxCompositionLayerView layer = _selectedLayer();
            VfxSourceElementCatalog catalog = _catalog();
            VfxSourceElementView element = catalog?.Find(layer?.ElementId);
            GameObject instance = layer == null || element == null ? null : VfxCompositionCandidateBaker.CreateLayer(catalog, element);
            try
            {
                if (instance == null)
                {
                    _compiled = null;
                    _resolved = null;
                    return;
                }
                _compiled = VfxSemanticParticleSystemAdapter.CompileAndApply(instance, layer, catalog, element, _registry, apply: false, allowSuggestedProfiles: true, useExternalLayerScheduler: false, out _resolved);
                RefreshCompiledUi(layer, element);
            }
            finally
            {
                if (instance != null) Object.DestroyImmediate(instance);
            }
        }

        private void RefreshCompiledUi(VfxCompositionLayerView layer, VfxSourceElementView element)
        {
            VfxTimeSegmentsView target = _compiled?.TargetSegments ?? layer.Timing.SourceSegments ?? new VfxTimeSegmentsView();
            bool playbackWindow = string.Equals(layer.Timing.RetimeMode, nameof(VfxRetimeMode.PlaybackWindow), StringComparison.Ordinal);
            ConfigureTimingSliderRanges(layer, target);
            _binding = true;
            try
            {
                _preRoll.value = target.PreRollFrames;
                _entry.value = target.EntryFrames;
                _body.value = target.BodyFrames;
                _minimumBody.SetValueWithoutNotify(ResolveMinimumBodyFrames(layer, target));
                _exit.value = target.ExitFrames;
                _cleanup.value = target.CleanupFrames;
                _cycle.value = target.CycleFrames;
                _targetFrames.SetValueWithoutNotify(ResolveTargetFrameFieldValue(layer, target));
                SetTimingSliderLabels(target, _targetFrames.value);
                if (playbackWindow)
                    _targetFrames.label = "播放窗口 · " + _targetFrames.value + " 帧";
                int sourceFrames = Math.Max(1, layer.Timing.SourceSegments?.ArtisticFrames ?? layer.EstimatedSourceFrames);
                _playbackSourceLength.text = "源内容长度：" + sourceFrames + " 帧 · 子 VFX 保持原速和原始时间参数";
                UnityVfxLayerWindowEndPolicy endPolicy = ResolveWindowEndPolicy(layer);
                _windowEndPolicy.SetValueWithoutNotify(WindowEndPolicyLabel(endPolicy));
                _dissipateFrames.highValue = Math.Max(1, _targetFrames.value);
                _dissipateFrames.SetValueWithoutNotify(Mathf.Clamp(layer.Timing.DissipateFrames, 0, _targetFrames.value));
                _dissipateFrames.SetEnabled(endPolicy == UnityVfxLayerWindowEndPolicy.DissipateThenClear);
                _dissipateFrames.label = "消散预留 · " + _dissipateFrames.value + " 帧";
                VfxVisualStartMode mode = ResolveVisualStartMode(layer);
                _visualStartMode.SetValueWithoutNotify(VisualStartLabel(mode));
                _manualLeadIn.SetValueWithoutNotify(Math.Max(0, layer.VisualStart?.ManualLeadInFrames ?? 0));
                _manualLeadIn.SetEnabled(mode == VfxVisualStartMode.ManualLeadIn);
                _visualStartSummary.text = _compiled == null
                    ? "源前导空白：待分析"
                    : "源前导空白：" + _compiled.SourceLeadInFrames + " 帧 · 最终首个可见帧：" + _compiled.ResolvedLeadInFrames + " 帧";
                _minimumLength.text = "最短艺术长度：" + target.MinimumArtisticFrames + " 帧 · 技术清理至 " + target.TechnicalFrames + " 帧";
            }
            finally { _binding = false; }
            UpdateSegmentBar(target);
            SetPlaybackWindowVisibility(playbackWindow);
            SetTargetFrameValidation(ResolveTargetFrameError(layer, _compiled));
            _members.Clear();
            if (_resolved != null) _members.AddRange(_resolved.Members);
            RefreshEditScopeChoices();
            SetSemanticFields(layer);
            IEnumerable<VfxResolvedComponentAuthoringProfile> materialMembers = EditingMember() is { } editingMember
                ? new[] { editingMember }
                : _members;
            bool materialTimeSupported = materialMembers.Any(member =>
                VfxSemanticParticleSystemAdapter.BindingsForProperty(member, "timing.materialLocalTime.scale")
                    .Any(binding => !binding.ReadOnly));
            _materialTimeScale.SetEnabled(materialTimeSupported);
            _materialTimeScale.tooltip = materialTimeSupported
                ? "按已确认 Shader Profile 的局部时间绑定缩放；1 表示保持 Profile 编译结果。"
                : "当前成员没有已确认的材质局部时间绑定；请先确认 Shader Profile。";
            SetRendererFields(layer);
            RefreshMemberMatrix();
            _memberList.style.height = Mathf.Clamp((_members.Count * 72) + 2, 74, 218);
            _memberList.Rebuild();
            BindMinMaxEditor(layer, loadSourceWhenMissing: false);
            BindGradientEditor(layer, loadSource: false);
            BindEmissionWindow(layer);
            var profileService = new VfxSemanticProfileConfirmationService();
            VfxProfileImpactAnalysisView coverage = profileService.Analyze(element, new VfxCompositionDraftService(), _resolved);
            VfxProfileImpactAnalysisView impact = profileService.Analyze(element, new VfxCompositionDraftService(), _resolved, _editMemberId);
            _profileCoverage.text = coverage.CoverageSummary;
            _profileCoverage.EnableInClassList("warning", coverage.ConfirmedComponentCount < coverage.TotalComponentCount);
            _profileImpact.text = impact.Summary;
            _confirmProfile.text = string.IsNullOrWhiteSpace(_editMemberId)
                ? "发布为共享 Element Profile…"
                : "发布当前成员共享 Profile…";
            _propertyCapabilityRows.Clear();
            _propertyCapabilityRows.AddRange(VfxSemanticPropertyCapabilityAnalyzer.Analyze(_registry, _members, _editMemberId));
            int confirmedCapabilities = _propertyCapabilityRows.Count(value => value.State == VfxSemanticPropertyCapabilityState.Confirmed);
            int partialCapabilities = _propertyCapabilityRows.Count(value => value.State == VfxSemanticPropertyCapabilityState.Partial);
            int previewCapabilities = _propertyCapabilityRows.Count(value => value.State == VfxSemanticPropertyCapabilityState.PreviewOnly);
            _propertyCapabilitySummary.text = "当前编辑目标：" + confirmedCapabilities + " 项共享绑定 · "
                + partialCapabilities + " 项部分成员 · " + previewCapabilities + " 项候选绑定 · "
                + (_propertyCapabilityRows.Count - confirmedCapabilities - partialCapabilities - previewCapabilities) + " 项只读/不支持/预留";
            _propertyCapabilityList.Rebuild();
            ApplySemanticControlCapabilities();
            _rawRows.Clear();
            for (int i = 0; i < _members.Count; i++) AddRawRows(_members[i]);
            _rawList.Rebuild();
            _diffRows.Clear();
            if (_compiled != null) _diffRows.AddRange(_compiled.FieldDiffs);
            _diffList.Rebuild();
            _compileHash.text = "Compile hash：" + (_compiled?.CompileHash ?? "—");
            _compileDiagnostics.text = _compiled == null || _compiled.Diagnostics.Count == 0
                ? "0 条诊断 · 可预览"
                : string.Join("\n", _compiled.Diagnostics.Select(value => value.Code + " · " + value.Message));
            _compileDiagnostics.EnableInClassList("warning", _compiled?.Diagnostics.Count > 0);
            _curveCanvas.MarkDirtyRepaint();
        }

        private void ConfigureTimingSliderRanges(VfxCompositionLayerView layer, VfxTimeSegmentsView segments)
        {
            int requestedFrames = Math.Max(1, layer?.Timing?.TargetArtisticFrames ?? 0);
            int sourceFrames = Math.Max(1, layer?.Timing?.SourceSegments?.TechnicalFrames ?? 0);
            int compiledFrames = Math.Max(1, segments?.TechnicalFrames ?? 0);
            int maximum = Math.Max(60, Math.Max(requestedFrames, Math.Max(sourceFrames, compiledFrames)) * 2);
            _targetFrames.lowValue = 1;
            _targetFrames.highValue = maximum;
            _minimumBody.lowValue = 1;
            _minimumBody.highValue = maximum;
            ProgressBar[] derivedSegments = { _preRoll, _entry, _body, _exit, _cleanup, _cycle };
            for (int i = 0; i < derivedSegments.Length; i++)
            {
                derivedSegments[i].lowValue = 0;
                derivedSegments[i].highValue = maximum;
            }
        }

        private void SetTimingSliderLabels(VfxTimeSegmentsView segments, int targetFrames)
        {
            _targetFrames.label = "目标艺术长度 · " + targetFrames + " 帧";
            _preRoll.title = "Pre-roll · " + segments.PreRollFrames + " 帧";
            _entry.title = "淡入 · " + segments.EntryFrames + " 帧";
            _body.title = "主体 · " + segments.BodyFrames + " 帧";
            _minimumBody.label = "最短主体 · " + _minimumBody.value + " 帧";
            _exit.title = "淡出 · " + segments.ExitFrames + " 帧";
            _cleanup.title = "清理 · " + segments.CleanupFrames + " 帧";
            _cycle.title = "周期 · " + segments.CycleFrames + " 帧";
        }

        private void SetPlaybackWindowVisibility(bool visible)
        {
            _playbackSourceLength.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            _windowEndPolicy.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            _dissipateFrames.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            _materialTimeScale.style.display = visible ? DisplayStyle.None : DisplayStyle.Flex;
            _segmentGrid.style.display = visible ? DisplayStyle.None : DisplayStyle.Flex;
            _minimumLength.style.display = visible ? DisplayStyle.None : DisplayStyle.Flex;
            _timeSegmentBar.style.display = visible ? DisplayStyle.None : DisplayStyle.Flex;
        }

        internal static int ResolveMinimumBodyFrames(
            VfxCompositionLayerView layer,
            VfxTimeSegmentsView compiledTarget)
        {
            int authored = layer?.Timing?.MinimumBodyFrames ?? 0;
            return authored > 0
                ? authored
                : Math.Max(1, compiledTarget?.MinimumBodyFrames ?? 1);
        }

        internal static int ResolveTargetFrameFieldValue(
            VfxCompositionLayerView layer,
            VfxTimeSegmentsView compiledTarget)
        {
            int requestedFrames = layer?.Timing?.TargetArtisticFrames ?? 0;
            return requestedFrames > 0
                ? requestedFrames
                : Math.Max(1, compiledTarget?.ArtisticFrames ?? 0);
        }

        internal static string ResolveTargetFrameError(
            VfxCompositionLayerView layer,
            VfxCompiledLayerView compiled)
        {
            if (compiled?.Diagnostics == null) return string.Empty;
            VfxSemanticDiagnosticView diagnostic = compiled.Diagnostics.FirstOrDefault(value =>
                string.Equals(value.Severity, "Error", StringComparison.Ordinal)
                && (value.Code == "VFXRET002"
                    || value.Code == "VFXRET010"
                    || value.Code == "VFXRET021"
                    || value.Code == "VFXRET031"
                    || value.Code == "VFXRET050"
                    || value.Code == "VFXRET060"
                    || value.Code == "VFXRET080"));
            if (diagnostic == null) return string.Empty;
            if (diagnostic.Code != "VFXRET060") return diagnostic.Code + "：" + diagnostic.Message;

            int sourceFrames = Math.Max(1, compiled.SourceSegments?.ArtisticFrames ?? 0);
            int requestedFrames = Math.Max(1, layer?.Timing?.TargetArtisticFrames ?? 0);
            return diagnostic.Code + "：Fixed 模式要求目标艺术长度等于源长度 "
                + sourceFrames + " 帧；当前为 " + requestedFrames + " 帧。";
        }

        private void SetTargetFrameValidation(string message)
        {
            bool hasError = !string.IsNullOrWhiteSpace(message);
            _targetFrames.EnableInClassList("field-error", hasError);
            _targetFrames.tooltip = hasError ? message : string.Empty;
            _targetFramesError.text = message;
            _targetFramesError.EnableInClassList("visible", hasError);
        }

        private static void ClearDerivedTimingOverrides(VfxCompositionLayerView layer)
        {
            layer?.SemanticOverrides?.RemoveAll(value => DerivedTimingPropertyIds.Contains(value.PropertyId));
            if (layer?.MemberOverrides == null) return;
            for (int i = layer.MemberOverrides.Count - 1; i >= 0; i--)
            {
                VfxMemberSemanticOverrideView member = layer.MemberOverrides[i];
                member?.Overrides?.RemoveAll(value => DerivedTimingPropertyIds.Contains(value.PropertyId));
                if ((member?.Overrides?.Count ?? 0) == 0 && string.IsNullOrWhiteSpace(member?.RetimeMode))
                    layer.MemberOverrides.RemoveAt(i);
            }
        }

        private void ApplySemanticControlCapabilities()
        {
            SetControlCapability(_materialTimeScale, "timing.materialLocalTime.scale");
            SetControlCapability(_opacity, "appearance.opacity");
            SetControlCapability(_primaryColor, "appearance.palette.primary");
            SetControlCapability(_secondaryColor, "appearance.palette.secondary");
            SetControlCapability(_detailColor, "appearance.palette.detail");
            SetControlCapability(_emissionColor, "appearance.emission.color");
            SetControlCapability(_emissionEv, "appearance.emission.exposureEv");
            SetControlCapability(_gradientEditor, "appearance.colorOverLifetime.exact");
            SetControlCapability(_emissionDensity, "emission.density");
            SetControlCapability(_burstScale, "emission.burst.scale");
            SetControlCapability(_emissionWindowStart, "emission.window.startFrame");
            SetControlCapability(_emissionWindowEnd, "emission.window.endFrame");
            SetControlCapability(_capacity, "emission.capacity.maxParticles");
            SetControlCapability(_motionSpeed, "motion.speed");
            SetControlCapability(_travelRange, "motion.travelRange");
            SetControlCapability(_gravity, "motion.gravity.world");
            SetControlCapability(_localGravity, "motion.gravity.local");
            SetControlCapability(_forceVector, "motion.force.vector");
            SetControlCapability(_drag, "motion.drag");
            SetControlCapability(_rotationSpeed, "motion.rotation.speed");
            SetControlCapability(_noiseStrength, "motion.noise.strength");
            SetControlCapability(_noiseFrequency, "motion.noise.frequency");
            SetControlCapability(_compositionScale, "shape.composition.scale");
            SetControlCapability(_particleSize, "shape.particle.size");
            SetControlCapability(_emitterScale, "shape.emitter.scale");
            SetControlCapability(_shapeRadius, "shape.radius");
            SetControlCapability(_shapeAngle, "shape.angle");
            SetControlCapability(_shapeArc, "shape.arc");
            SetControlCapability(_directionSpread, "shape.direction.spread");
            SetControlCapability(_renderMode, "render.renderer.mode");
            SetControlCapability(_renderAlignment, "render.renderer.alignment");
            SetControlCapability(_renderSortMode, "render.renderer.sortMode");
            SetControlCapability(_renderPivot, "render.renderer.pivot");
            SetControlCapability(_renderMinSize, "render.renderer.minParticleSize");
            SetControlCapability(_renderMaxSize, "render.renderer.maxParticleSize");
            SetControlCapability(_renderMaterial, "render.renderer.material");
            SetControlCapability(_renderMesh, "render.renderer.mesh");
            SetControlCapability(_textureMain, "render.texture.main");
            SetControlCapability(_textureMask, "render.texture.mask");
            SetControlCapability(_textureNoise, "render.texture.noise");
            SetControlCapability(_textureRamp, "render.texture.ramp");
            SetControlCapability(_textureNormal, "render.texture.normal");
            SetControlCapability(_textureFlipbook, "render.texture.flipbook");
            SetControlCapability(_renderQueue, "render.state.renderQueue");
            SetControlCapability(_renderCull, "render.state.cull");
            SetControlCapability(_renderZWrite, "render.state.zWrite");
            SetControlCapability(_renderSrcBlend, "render.state.srcBlend");
            SetControlCapability(_renderDstBlend, "render.state.dstBlend");
            SetControlCapability(_sortingOffset, "render.sortingOrder.offset");
            SetControlCapability(_flipbookSpeed, "render.flipbook.speed");
            SetControlCapability(_flipbookCycles, "render.flipbook.cycles");
            ApplyCurveCapability();
        }

        private void ApplyCurveCapability()
        {
            string propertyId = CurvePropertyId(_curveProperty.value);
            VfxSemanticPropertyCapabilityRow capability = Capability(propertyId);
            bool supported = capability == null || IsEditableCapability(capability.State);
            bool exactOnly = IsExactMinMaxOnlyProperty(propertyId);
            _curveEditor.SetEnabled(supported && !exactOnly);
            _curveOperation.SetEnabled(supported && !exactOnly);
            _minMaxEditor.SetEnabled(supported);
            string tooltip = capability == null ? string.Empty : CapabilityTooltip(capability);
            _curveEditor.tooltip = tooltip;
            _curveOperation.tooltip = tooltip;
            _minMaxEditor.tooltip = tooltip;
        }

        private void SetControlCapability(VisualElement control, string propertyId)
        {
            if (control == null) return;
            VfxSemanticPropertyCapabilityRow capability = Capability(propertyId);
            if (capability == null) return;
            control.SetEnabled(IsEditableCapability(capability.State));
            control.tooltip = CapabilityTooltip(capability);
        }

        private VfxSemanticPropertyCapabilityRow Capability(string propertyId)
        {
            return _propertyCapabilityRows.FirstOrDefault(value => string.Equals(value.PropertyId, propertyId, StringComparison.Ordinal));
        }

        private static bool IsEditableCapability(VfxSemanticPropertyCapabilityState state)
        {
            return state == VfxSemanticPropertyCapabilityState.Confirmed
                || state == VfxSemanticPropertyCapabilityState.Partial
                || state == VfxSemanticPropertyCapabilityState.PreviewOnly;
        }

        private static string CapabilityTooltip(VfxSemanticPropertyCapabilityRow capability)
        {
            string prefix = capability.State switch
            {
                VfxSemanticPropertyCapabilityState.Confirmed => "可安全写入并烘焙。",
                VfxSemanticPropertyCapabilityState.Partial => "仅部分成员可安全写入；请检查成员矩阵。",
                VfxSemanticPropertyCapabilityState.PreviewOnly => "可使用自动建议编辑和烘焙；本次绑定会冻结在 Candidate 回执中，尚未成为共享 Profile。",
                VfxSemanticPropertyCapabilityState.ReadOnly => "当前 Profile 只读。",
                VfxSemanticPropertyCapabilityState.Reserved => "预留属性，当前不可写。",
                _ => "当前成员没有安全 Binding/Adapter。"
            };
            return prefix + "\n" + capability.PropertyId + "\n" + capability.Reason;
        }

        private void UpdateSegmentBar(VfxTimeSegmentsView segments)
        {
            int total = Math.Max(1, segments.TechnicalFrames);
            int[] frames = { segments.PreRollFrames, segments.EntryFrames, segments.BodyFrames, segments.ExitFrames, segments.CleanupFrames };
            for (int i = 0; i < _segmentBars.Length; i++)
                _segmentBars[i].style.flexGrow = Math.Max(0.01f, frames[i] / (float)total);
        }

        private void ResetToSource()
        {
            if (!string.IsNullOrWhiteSpace(_editMemberId))
            {
                string memberId = _editMemberId;
                Change(layer =>
                {
                    VfxMemberSemanticOverrideView member = layer.MemberOverrides?.FirstOrDefault(value =>
                        string.Equals(value.MemberId, memberId, StringComparison.Ordinal));
                    if (member == null) return;
                    string[] propertyIds = member.Overrides?
                        .Select(value => value.PropertyId)
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Distinct(StringComparer.Ordinal)
                        .ToArray() ?? Array.Empty<string>();
                    layer.MemberOverrides.Remove(member);
                    for (int i = 0; i < propertyIds.Length; i++)
                        SetPropertyParticipation(layer, memberId, propertyIds[i], VfxMemberParticipationState.Follow);
                });
                return;
            }
            Change(layer =>
            {
                layer.SemanticOverrides.Clear();
                layer.MemberOverrides.Clear();
                layer.MemberParticipation.Clear();
                layer.Timing.RetimeMode = nameof(VfxRetimeMode.PlaybackWindow);
                layer.Timing.TargetArtisticFrames = layer.Timing.SourceSegments?.ArtisticFrames ?? Math.Max(1, layer.EstimatedSourceFrames);
                layer.Timing.WindowEndPolicy = nameof(UnityVfxLayerWindowEndPolicy.StopEmitting);
                layer.Timing.DissipateFrames = 0;
                layer.Timing.EventMap.Clear();
                layer.Timing.ExperimentalAcknowledged = false;
                layer.VisualStart = null;
            });
        }

        private static VfxVisualStartMode ResolveVisualStartMode(VfxCompositionLayerView layer)
        {
            return Enum.TryParse(layer?.VisualStart?.Mode, false, out VfxVisualStartMode mode)
                ? mode
                : VfxVisualStartMode.AutoNormalize;
        }

        private static string VisualStartLabel(VfxVisualStartMode mode)
        {
            return mode switch
            {
                VfxVisualStartMode.PreserveSource => "保留源时间",
                VfxVisualStartMode.ManualLeadIn => "手动前导",
                _ => "自动移除公共空白"
            };
        }

        private static VfxVisualStartMode VisualStartMode(string label)
        {
            return label switch
            {
                "保留源时间" => VfxVisualStartMode.PreserveSource,
                "手动前导" => VfxVisualStartMode.ManualLeadIn,
                _ => VfxVisualStartMode.AutoNormalize
            };
        }

        private static UnityVfxLayerWindowEndPolicy ResolveWindowEndPolicy(VfxCompositionLayerView layer)
        {
            return Enum.TryParse(layer?.Timing?.WindowEndPolicy, false, out UnityVfxLayerWindowEndPolicy policy)
                ? policy
                : UnityVfxLayerWindowEndPolicy.StopEmitting;
        }

        private static string RetimeModeLabel(string value)
        {
            return Enum.TryParse(value, false, out VfxRetimeMode mode)
                ? mode switch
                {
                    VfxRetimeMode.PlaybackWindow => "原速裁切",
                    VfxRetimeMode.ExtendBody => "伸缩主体",
                    VfxRetimeMode.ExtendTail => "调整尾迹",
                    VfxRetimeMode.RepeatBody => "重复主体",
                    VfxRetimeMode.EventRemap => "事件重排",
                    VfxRetimeMode.WholeSpeed => "整体变速",
                    _ => "固定源长度"
                }
                : "固定源长度";
        }

        private static VfxRetimeMode RetimeMode(string label)
        {
            return label switch
            {
                "原速裁切" => VfxRetimeMode.PlaybackWindow,
                "伸缩主体" => VfxRetimeMode.ExtendBody,
                "调整尾迹" => VfxRetimeMode.ExtendTail,
                "重复主体" => VfxRetimeMode.RepeatBody,
                "事件重排" => VfxRetimeMode.EventRemap,
                "整体变速" => VfxRetimeMode.WholeSpeed,
                _ => VfxRetimeMode.Fixed
            };
        }

        private static string WindowEndPolicyLabel(UnityVfxLayerWindowEndPolicy policy)
        {
            return policy switch
            {
                UnityVfxLayerWindowEndPolicy.StopAndClear => "立即清理",
                UnityVfxLayerWindowEndPolicy.DissipateThenClear => "消散后清理",
                _ => "自然消散"
            };
        }

        private static UnityVfxLayerWindowEndPolicy WindowEndPolicy(string label)
        {
            return label switch
            {
                "立即清理" => UnityVfxLayerWindowEndPolicy.StopAndClear,
                "消散后清理" => UnityVfxLayerWindowEndPolicy.DissipateThenClear,
                _ => UnityVfxLayerWindowEndPolicy.StopEmitting
            };
        }

        private void ConfirmSharedProfile()
        {
            VfxCompositionLayerView layer = _selectedLayer();
            VfxSourceElementCatalog catalog = _catalog();
            VfxSourceElementView element = catalog?.Find(layer?.ElementId);
            if (layer == null || element == null || _resolved == null) return;
            var service = new VfxSemanticProfileConfirmationService();
            VfxProfileImpactAnalysisView impact = service.Analyze(element, new VfxCompositionDraftService(), _resolved, _editMemberId);
            if (!EditorUtility.DisplayDialog(
                    "发布共享 VFX Profile",
                    impact.Summary
                    + "\n\n这是可选的跨草稿复用操作，会修改仓库内共享 Profile。"
                    + "\n当前 Candidate 烘焙不依赖共享 Profile；Draft 保存也是另一个独立操作。",
                    "发布共享 Profile",
                    "取消")) return;
            _recordSharedChange();
            VfxProfileManifestView manifest = new VfxSemanticProfileStore().LoadManifest(out _);
            bool success = service.Confirm(element, _resolved, layer, manifest?.ManifestHash ?? string.Empty, _editMemberId, out string message);
            _setStatus(message, !success);
            if (success) Bind(layer);
        }

        private void CopyProfileGapReport()
        {
            VfxCompositionLayerView layer = _selectedLayer();
            VfxSourceElementView element = _catalog()?.Find(layer?.ElementId);
            if (element == null || _resolved == null)
            {
                _setStatus("请先选择并解析一个 Element。", true);
                return;
            }
            VfxProfileManifestView manifest = new VfxSemanticProfileStore().LoadManifest(out string manifestMessage);
            if (manifest == null)
            {
                _setStatus(manifestMessage, true);
                return;
            }
            VfxSemanticProfileGapReportView report =
                VfxSemanticProfileGapReportBuilder.Build(element, _resolved, _registry, manifest);
            EditorGUIUtility.systemCopyBuffer = report.ToStableJson();
            _setStatus("已复制 Profile 缺口报告：" + report.SemanticBakeBlockers.Count + " 个语义烘焙阻断，"
                + report.CapabilityLimitations.Count + " 类能力限制。", false);
        }

        private void ShowPage(int index)
        {
            for (int i = 0; i < _pages.Length; i++)
            {
                bool active = i == index;
                _pages[i].EnableInClassList("active", active);
                _tabs[i].EnableInClassList("active", active);
            }
        }

        private VisualElement MakeMemberRow()
        {
            var row = new VisualElement();
            row.AddToClassList("semantic-member-row");
            var name = new Label { name = "member-name" }; name.AddToClassList("semantic-member-name"); row.Add(name);
            var meta = new Label { name = "member-meta" }; meta.AddToClassList("semantic-member-meta"); row.Add(meta);
            var state = new DropdownField { name = "member-state", choices = ParticipationChoices.ToList() }; state.AddToClassList("semantic-member-state"); row.Add(state);
            state.RegisterValueChangedCallback(evt =>
            {
                if (_binding || row.userData is not VfxResolvedComponentAuthoringProfile member) return;
                string propertyId = SelectedMemberPropertyId();
                Change(layer =>
                {
                    if (string.IsNullOrWhiteSpace(propertyId)) SetParticipation(layer, member.MemberId, ParseParticipation(evt.newValue));
                    else SetPropertyParticipation(layer, member.MemberId, propertyId, ParseParticipation(evt.newValue));
                });
            });
            return row;
        }

        private void RefreshMemberMatrix()
        {
            if (_memberMatrix == null) return;
            _memberMatrix.Clear();
            _memberSummary.text = _members.Count == 0
                ? "当前 Layer 没有可配置的 ParticleSystem 成员。"
                : _members.Count + " 个成员 · 跟随表示使用 Layer 主控，覆盖表示使用成员独立值。";
            _memberMatrix.style.height = _members.Count == 0
                ? 96
                : Mathf.Clamp((_members.Count * 158) + 8, 166, 380);
            if (_members.Count == 0)
            {
                var empty = new Label("没有可显示的成员");
                empty.AddToClassList("semantic-matrix-empty");
                _memberMatrix.Add(empty);
                return;
            }

            for (int memberIndex = 0; memberIndex < _members.Count; memberIndex++)
            {
                VfxResolvedComponentAuthoringProfile member = _members[memberIndex];
                var card = new VisualElement();
                card.AddToClassList("semantic-matrix-card");
                var header = new VisualElement();
                header.AddToClassList("semantic-matrix-card-header");
                var identity = new VisualElement();
                identity.AddToClassList("semantic-matrix-identity");
                var name = new Label(member.Source.DisplayName) { tooltip = member.MemberId + "\n" + member.Profile.ProfileId };
                name.AddToClassList("semantic-matrix-name");
                identity.Add(name);
                string componentStatus = member.Persisted ? "Component 已确认" : "Component 建议";
                string shaderStatus = member.ShaderProfile == null
                    ? "无 Shader Profile"
                    : member.ShaderProfilePersisted ? "Shader 已确认" : "Shader 建议";
                var meta = new Label(string.Join(" / ", member.Source.Roles) + " · " + componentStatus + " · " + shaderStatus + " · " + member.Profile.Timing.RetimeMechanism);
                meta.AddToClassList("semantic-matrix-meta");
                identity.Add(meta);
                header.Add(identity);
                header.Add(CreateMemberParticipationField(member, MatrixProperties[0].PropertyId, MatrixProperties[0].Label, "semantic-matrix-total"));
                card.Add(header);

                var propertyGrid = new VisualElement();
                propertyGrid.AddToClassList("semantic-matrix-property-grid");
                propertyGrid.Add(CreateMemberRetimeField(member));
                for (int propertyIndex = 1; propertyIndex < MatrixProperties.Length; propertyIndex++)
                {
                    propertyGrid.Add(CreateMemberParticipationField(
                        member,
                        MatrixProperties[propertyIndex].PropertyId,
                        MatrixProperties[propertyIndex].Label,
                        "semantic-matrix-property"));
                }
                card.Add(propertyGrid);
                _memberMatrix.Add(card);
            }
        }

        private DropdownField CreateMemberRetimeField(VfxResolvedComponentAuthoringProfile member)
        {
            var choices = new List<string> { "跟随 Layer" };
            choices.AddRange(RetimeChoices);
            VfxMemberSemanticOverrideView memberOverride = _selectedLayer()?.MemberOverrides?.FirstOrDefault(value =>
                value.MemberId == member.MemberId);
            var field = new DropdownField
            {
                label = "重定时",
                choices = choices,
                tooltip = "默认跟随 Layer 主模式；仅在该成员需要不同时间处理时设置独立模式。"
            };
            field.AddToClassList("semantic-matrix-property");
            field.AddToClassList("semantic-matrix-retime");
            field.SetValueWithoutNotify(string.IsNullOrWhiteSpace(memberOverride?.RetimeMode)
                ? choices[0]
                : memberOverride.RetimeMode);
            string memberId = member.MemberId;
            field.RegisterValueChangedCallback(evt =>
            {
                if (_binding) return;
                Change(layer => SetMemberRetimeMode(
                    layer,
                    memberId,
                    evt.newValue == choices[0] ? null : evt.newValue));
            });
            return field;
        }

        private DropdownField CreateMemberParticipationField(
            VfxResolvedComponentAuthoringProfile member,
            string propertyId,
            string label,
            string className)
        {
            bool supported = string.IsNullOrWhiteSpace(propertyId)
                || VfxSemanticParticleSystemAdapter.BindingsForProperty(member, propertyId).Any(value => !value.ReadOnly);
            VfxMemberParticipationState participation = GetParticipation(_selectedLayer(), _resolved?.ElementProfile, member.MemberId, propertyId);
            if (!supported && participation == VfxMemberParticipationState.Follow) participation = VfxMemberParticipationState.Unsupported;
            var field = new DropdownField
            {
                label = label,
                choices = ParticipationChoices.ToList(),
                tooltip = label + " · " + member.Source.DisplayName
            };
            field.AddToClassList(className);
            field.EnableInClassList("unsupported", !supported);
            field.SetValueWithoutNotify(ParticipationLabel(participation));
            field.SetEnabled(supported);
            string capturedMemberId = member.MemberId;
            string capturedPropertyId = propertyId;
            field.RegisterValueChangedCallback(evt =>
            {
                if (_binding) return;
                Change(layer =>
                {
                    if (string.IsNullOrWhiteSpace(capturedPropertyId)) SetParticipation(layer, capturedMemberId, ParseParticipation(evt.newValue));
                    else SetPropertyParticipation(layer, capturedMemberId, capturedPropertyId, ParseParticipation(evt.newValue));
                });
            });
            return field;
        }

        private void BindMemberRow(VisualElement row, int index)
        {
            VfxResolvedComponentAuthoringProfile member = _members[index];
            row.userData = member;
            row.Q<Label>("member-name").text = member.Source.DisplayName;
            string propertyId = SelectedMemberPropertyId();
            bool supported = string.IsNullOrWhiteSpace(propertyId) || VfxSemanticParticleSystemAdapter.BindingsForProperty(member, propertyId).Any(value => !value.ReadOnly);
            string support = string.IsNullOrWhiteSpace(propertyId) ? string.Empty : supported ? " · 可写" : " · 无绑定";
            row.Q<Label>("member-meta").text = string.Join(" / ", member.Source.Roles) + " · " + member.Profile.Status + " · " + member.Profile.Timing.RetimeMechanism + support;
            VfxMemberParticipationState participation = GetParticipation(_selectedLayer(), _resolved?.ElementProfile, member.MemberId, propertyId);
            if (!supported && participation == VfxMemberParticipationState.Follow) participation = VfxMemberParticipationState.Unsupported;
            string state = ParticipationLabel(participation);
            _binding = true;
            row.Q<DropdownField>("member-state").SetValueWithoutNotify(state);
            _binding = false;
        }

        private string SelectedMemberPropertyId()
        {
            int index = _memberProperty?.index ?? 0;
            return index >= 0 && index < _memberPropertyIds.Count ? _memberPropertyIds[index] : string.Empty;
        }

        private static VisualElement MakeRawRow()
        {
            var row = new VisualElement(); row.AddToClassList("semantic-raw-row");
            row.Add(new Label { name = "raw-path" });
            var value = new Label { name = "raw-value" }; value.AddToClassList("semantic-member-meta"); row.Add(value);
            return row;
        }

        private void BindRawRow(VisualElement row, int index)
        {
            row.Q<Label>("raw-path").text = _rawRows[index].Path;
            row.Q<Label>("raw-value").text = _rawRows[index].Value;
        }

        private static VisualElement MakeDiffRow()
        {
            var row = new VisualElement(); row.AddToClassList("semantic-diff-row");
            var path = new Label { name = "diff-path" }; path.AddToClassList("semantic-diff-path"); row.Add(path);
            var values = new Label { name = "diff-values" }; values.AddToClassList("semantic-diff-values"); row.Add(values);
            return row;
        }

        private void BindDiffRow(VisualElement row, int index)
        {
            VfxUnityFieldDiffView diff = _diffRows[index];
            row.Q<Label>("diff-path").text = diff.MemberId + " · " + diff.UnityPath;
            row.Q<Label>("diff-values").text = diff.SourceValue + "  →  " + diff.FinalValue + "   [" + diff.PropertyId + " / " + diff.Operation + " / " + diff.TimeDomain + "]";
        }

        private void AddRawRows(VfxResolvedComponentAuthoringProfile member)
        {
            VfxParticleSystemSnapshotView snapshot = member.Snapshot;
            if (snapshot == null) return;
            string prefix = member.Source.DisplayName + " · ";
            _rawRows.Add(new VfxRawFieldRow(prefix + "Main.duration", snapshot.DurationSeconds + " s"));
            _rawRows.Add(new VfxRawFieldRow(prefix + "Main.startDelay", Curve(snapshot.StartDelay)));
            _rawRows.Add(new VfxRawFieldRow(prefix + "Main.startLifetime", Curve(snapshot.StartLifetime)));
            _rawRows.Add(new VfxRawFieldRow(prefix + "Main.startSpeed", Curve(snapshot.StartSpeed)));
            _rawRows.Add(new VfxRawFieldRow(prefix + "Main.startSize", Curve(snapshot.StartSize)));
            _rawRows.Add(new VfxRawFieldRow(prefix + "Main.gravityModifier", Curve(snapshot.GravityModifier)));
            _rawRows.Add(new VfxRawFieldRow(prefix + "Emission.rateOverTime", Curve(snapshot.RateOverTime)));
            _rawRows.Add(new VfxRawFieldRow(prefix + "Emission.rateOverDistance", Curve(snapshot.RateOverDistance)));
            _rawRows.Add(new VfxRawFieldRow(prefix + "Shape", "type " + snapshot.ShapeType + " · radius " + snapshot.ShapeRadius.ToString("0.###") + " · angle " + snapshot.ShapeAngle.ToString("0.###") + " · arc " + snapshot.ShapeArc.ToString("0.###")));
            _rawRows.Add(new VfxRawFieldRow(prefix + "Velocity.speedModifier", Curve(snapshot.VelocitySpeedModifier)));
            _rawRows.Add(new VfxRawFieldRow(prefix + "Force.xyz", Curve(snapshot.ForceOverLifetimeX) + " / " + Curve(snapshot.ForceOverLifetimeY) + " / " + Curve(snapshot.ForceOverLifetimeZ)));
            _rawRows.Add(new VfxRawFieldRow(prefix + "LimitVelocity.drag", Curve(snapshot.Drag)));
            _rawRows.Add(new VfxRawFieldRow(prefix + "Noise", Curve(snapshot.NoiseStrength) + " · frequency " + snapshot.NoiseFrequency.ToString("0.###")));
            if (snapshot.Trails?.Enabled == true)
            {
                _rawRows.Add(new VfxRawFieldRow(
                    prefix + "Trails.lifetime",
                    Curve(snapshot.Trails.Lifetime)
                    + " · ratio " + snapshot.Trails.Ratio.ToString("0.###")
                    + " · dieWithParticles " + snapshot.Trails.DieWithParticles));
                _rawRows.Add(new VfxRawFieldRow(
                    prefix + "Trails.width/color",
                    Curve(snapshot.Trails.WidthOverTrail)
                    + " · inheritColor " + snapshot.Trails.InheritParticleColor
                    + " · worldSpace " + snapshot.Trails.WorldSpace));
                _rawRows.Add(new VfxRawFieldRow(
                    prefix + "Trails.contract",
                    member.Profile?.Timing?.TrailContract?.RetimePolicy ?? "ReadOnly"));
            }
            string textureTiles = snapshot.TextureTiles != null && snapshot.TextureTiles.Count >= 2
                ? snapshot.TextureTiles[0] + "×" + snapshot.TextureTiles[1]
                : "未启用";
            _rawRows.Add(new VfxRawFieldRow(prefix + "TextureSheet", textureTiles + " · cycles " + snapshot.TextureCycleCount));
            _rawRows.Add(new VfxRawFieldRow(prefix + "Modules", string.Join(", ", snapshot.ModuleFlags)));
            _rawRows.Add(new VfxRawFieldRow(prefix + "Renderer", "mode " + snapshot.RendererMode + " · order " + snapshot.SortingOrder));
            _rawRows.Add(new VfxRawFieldRow(prefix + "Vertex Streams", string.Join(", ", snapshot.VertexStreams)));
            if (member.ShaderProfile != null)
            {
                _rawRows.Add(new VfxRawFieldRow(prefix + "Shader Profile", member.ShaderProfile.ShaderName + " · " + member.ShaderProfile.Status + " · " + member.ShaderProfile.ProfileId));
                for (int i = 0; i < member.ShaderProfile.CustomDataBindings.Count; i++)
                {
                    VfxShaderCustomDataBindingView binding = member.ShaderProfile.CustomDataBindings[i];
                    _rawRows.Add(new VfxRawFieldRow(prefix + "Custom Data " + binding.Stream + "." + binding.Channels, binding.ShaderUsage + (binding.Editable ? " · 可编辑" : " · 只读")));
                }
            }
        }

        private void DrawCurveTrack()
        {
            Rect rect = _curveCanvas.contentRect;
            if (rect.width < 4f || rect.height < 4f) return;
            EditorGUI.DrawRect(rect, new Color(0.043f, 0.067f, 0.086f));
            DrawGrid(rect);
            VfxTimeSegmentsView source = _compiled?.SourceSegments;
            VfxTimeSegmentsView target = _compiled?.TargetSegments;
            if (source == null || target == null) return;
            Handles.BeginGUI();
            if (TryResolveSourceCurve(out AnimationCurve sourceCurve, out float semanticFactor))
            {
                AnimationCurve modified = ScaleCurve(sourceCurve, semanticFactor);
                int sourceOffset = source.PreRollFrames;
                int targetOffset = target.PreRollFrames;
                AnimationCurve final = VfxSemanticRetimeCompiler.RemapNormalizedCurve(
                    modified,
                    _compiled.TimeMap,
                    Math.Max(1, source.ArtisticFrames - sourceOffset),
                    Math.Max(1, target.ArtisticFrames - targetOffset),
                    sourceOffset,
                    targetOffset);
                float maximum = CurveMaximum(sourceCurve, modified, final);
                DrawAnimationCurve(rect, sourceCurve, maximum, new Color(0.45f, 0.51f, 0.55f), 2f);
                DrawAnimationCurve(rect, modified, maximum, new Color(0.22f, 0.66f, 0.91f), 2f);
                DrawAnimationCurve(rect, final, maximum, new Color(0.95f, 0.97f, 0.98f), 2f);
            }
            else
            {
                DrawEnvelope(rect, source, new Color(0.45f, 0.51f, 0.55f), 2f);
                DrawEnvelope(rect, target, new Color(0.95f, 0.97f, 0.98f), 2f);
                DrawBodyDelta(rect, source, target, new Color(0.22f, 0.66f, 0.91f), 2f);
            }
            Handles.EndGUI();
        }

        private bool TryResolveSourceCurve(out AnimationCurve curve, out float semanticFactor)
        {
            curve = null;
            semanticFactor = 1f;
            VfxParticleSystemSnapshotView snapshot = EditingMember()?.Snapshot ?? _members.FirstOrDefault()?.Snapshot;
            if (snapshot == null) return false;
            VfxCompositionLayerView layer = _selectedLayer();
            switch (_curveProperty.value)
            {
                case "发射包络":
                    curve = SnapshotCurve(snapshot.RateOverTime);
                    semanticFactor = Float(layer, "emission.density", 1f);
                    break;
                case "粒子尺寸":
                    curve = SnapshotCurve(snapshot.StartSize);
                    semanticFactor = Float(layer, "shape.particle.size", 1f);
                    break;
                case "运动速度":
                    curve = SnapshotCurve(snapshot.StartSpeed);
                    semanticFactor = Float(layer, "motion.speed", 1f);
                    break;
                case "Noise 强度":
                    semanticFactor = Float(layer, "motion.noise.strength", 1f);
                    break;
            }
            return curve != null;
        }

        private void BindCurveEditor(VfxCompositionLayerView layer)
        {
            if (_curveEditor == null || _curveOperation == null) return;
            string propertyId = CurvePropertyId(_curveProperty.value);
            bool exactOnly = IsExactMinMaxOnlyProperty(propertyId);
            VfxSemanticOverrideView semantic = Find(layer, propertyId);
            AnimationCurve source = ResolveSourceCurveForEditor();
            AnimationCurve value = semantic?.Value?.Type == JTokenType.Object
                ? VfxCurveAuthoringJson.Restore(semantic.Value, source)
                : VfxCurveAuthoringJson.Clone(source) ?? AnimationCurve.Linear(0f, 1f, 1f, 1f);
            _binding = true;
            try
            {
                _curveEditor.SetValueWithoutNotify(value);
                _curveOperation.SetValueWithoutNotify(semantic?.Operation ?? nameof(VfxSemanticOperation.Replace));
                _curveEditor.SetEnabled(!exactOnly);
                _curveOperation.SetEnabled(!exactOnly);
            }
            finally { _binding = false; }
        }

        private void BindMinMaxEditor(VfxCompositionLayerView layer, bool loadSourceWhenMissing)
        {
            if (_minMaxEditor == null) return;
            string propertyId = CurvePropertyId(_curveProperty.value);
            bool supported = IsMinMaxCurveProperty(propertyId);
            _minMaxEditor.style.display = supported ? DisplayStyle.Flex : DisplayStyle.None;
            if (!supported) return;
            VfxSemanticOverrideView semantic = Find(layer, propertyId);
            ParticleSystem.MinMaxCurve source = ResolveSourceMinMaxCurve(propertyId);
            ParticleSystem.MinMaxCurve value = semantic != null && VfxMinMaxCurveAuthoringJson.IsMinMaxCurve(semantic.Value)
                ? VfxMinMaxCurveAuthoringJson.Restore(semantic.Value, source)
                : source;
            _binding = true;
            try
            {
                _minMaxMode.SetValueWithoutNotify(value.mode.ToString());
                _minMaxMultiplier.SetValueWithoutNotify(value.curveMultiplier);
                _minMaxConstantMin.SetValueWithoutNotify(value.constantMin);
                _minMaxConstantMax.SetValueWithoutNotify(value.constantMax);
                _minMaxCurveMin.SetValueWithoutNotify(VfxCurveAuthoringJson.Clone(value.curveMin) ?? AnimationCurve.Constant(0f, 1f, value.constantMin));
                _minMaxCurveMax.SetValueWithoutNotify(VfxCurveAuthoringJson.Clone(value.curveMax) ?? AnimationCurve.Constant(0f, 1f, value.constantMax));
                if (loadSourceWhenMissing) _minMaxEditor.value = true;
            }
            finally { _binding = false; }
            UpdateMinMaxModeVisibility();
        }

        private void ApplyExactMinMaxCurve()
        {
            if (_binding || !Enum.TryParse(_minMaxMode.value, false, out ParticleSystemCurveMode mode)) return;
            ParticleSystem.MinMaxCurve value;
            switch (mode)
            {
                case ParticleSystemCurveMode.Constant:
                    value = new ParticleSystem.MinMaxCurve(_minMaxConstantMax.value);
                    break;
                case ParticleSystemCurveMode.TwoConstants:
                    value = new ParticleSystem.MinMaxCurve(_minMaxConstantMin.value, _minMaxConstantMax.value);
                    break;
                case ParticleSystemCurveMode.Curve:
                    value = new ParticleSystem.MinMaxCurve(_minMaxMultiplier.value, VfxCurveAuthoringJson.Clone(_minMaxCurveMax.value) ?? AnimationCurve.Constant(0f, 1f, _minMaxConstantMax.value));
                    break;
                case ParticleSystemCurveMode.TwoCurves:
                    value = new ParticleSystem.MinMaxCurve(
                        _minMaxMultiplier.value,
                        VfxCurveAuthoringJson.Clone(_minMaxCurveMin.value) ?? AnimationCurve.Constant(0f, 1f, _minMaxConstantMin.value),
                        VfxCurveAuthoringJson.Clone(_minMaxCurveMax.value) ?? AnimationCurve.Constant(0f, 1f, _minMaxConstantMax.value));
                    break;
                default:
                    return;
            }
            Change(layer => Upsert(layer, CurvePropertyId(_curveProperty.value), VfxSemanticOperation.Replace, VfxMinMaxCurveAuthoringJson.Capture(value)));
        }

        private void UpdateMinMaxModeVisibility()
        {
            bool twoConstants = _minMaxMode.value == nameof(ParticleSystemCurveMode.TwoConstants);
            bool curve = _minMaxMode.value == nameof(ParticleSystemCurveMode.Curve);
            bool twoCurves = _minMaxMode.value == nameof(ParticleSystemCurveMode.TwoCurves);
            _minMaxMultiplier.style.display = curve || twoCurves ? DisplayStyle.Flex : DisplayStyle.None;
            _minMaxConstantMin.style.display = twoConstants ? DisplayStyle.Flex : DisplayStyle.None;
            _minMaxConstantMax.style.display = curve || twoCurves ? DisplayStyle.None : DisplayStyle.Flex;
            _minMaxCurveMin.style.display = twoCurves ? DisplayStyle.Flex : DisplayStyle.None;
            _minMaxCurveMax.style.display = curve || twoCurves ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void BindGradientEditor(VfxCompositionLayerView layer, bool loadSource)
        {
            if (_gradientEditor == null) return;
            const string propertyId = "appearance.colorOverLifetime.exact";
            VfxSemanticOverrideView semantic = Find(layer, propertyId);
            ParticleSystem.MinMaxGradient source = ResolveSourceGradient();
            ParticleSystem.MinMaxGradient value = semantic != null && VfxMinMaxGradientAuthoringJson.IsMinMaxGradient(semantic.Value)
                ? VfxMinMaxGradientAuthoringJson.Restore(semantic.Value, source)
                : source;
            _binding = true;
            try
            {
                _gradientMode.SetValueWithoutNotify(value.mode.ToString());
                _gradientColorMin.SetValueWithoutNotify(value.colorMin);
                _gradientColorMax.SetValueWithoutNotify(value.colorMax);
                _gradientMin.SetValueWithoutNotify(CloneGradient(value.gradientMin) ?? WhiteGradient());
                _gradientMax.SetValueWithoutNotify(CloneGradient(value.gradientMax) ?? WhiteGradient());
                if (loadSource) _gradientEditor.value = true;
            }
            finally { _binding = false; }
            UpdateGradientModeVisibility();
        }

        private void ApplyExactGradient()
        {
            if (_binding || !Enum.TryParse(_gradientMode.value, false, out ParticleSystemGradientMode mode)) return;
            ParticleSystem.MinMaxGradient value;
            switch (mode)
            {
                case ParticleSystemGradientMode.Color:
                    value = new ParticleSystem.MinMaxGradient(_gradientColorMax.value);
                    break;
                case ParticleSystemGradientMode.TwoColors:
                    value = new ParticleSystem.MinMaxGradient(_gradientColorMin.value, _gradientColorMax.value);
                    break;
                case ParticleSystemGradientMode.TwoGradients:
                    value = new ParticleSystem.MinMaxGradient(
                        CloneGradient(_gradientMin.value) ?? WhiteGradient(),
                        CloneGradient(_gradientMax.value) ?? WhiteGradient());
                    break;
                case ParticleSystemGradientMode.RandomColor:
                    value = new ParticleSystem.MinMaxGradient(CloneGradient(_gradientMax.value) ?? WhiteGradient())
                    {
                        mode = ParticleSystemGradientMode.RandomColor
                    };
                    break;
                default:
                    value = new ParticleSystem.MinMaxGradient(CloneGradient(_gradientMax.value) ?? WhiteGradient());
                    break;
            }
            Change(layer => Upsert(layer, "appearance.colorOverLifetime.exact", VfxSemanticOperation.Replace, VfxMinMaxGradientAuthoringJson.Capture(value)));
        }

        private void UpdateGradientModeVisibility()
        {
            if (_gradientMode == null) return;
            bool color = _gradientMode.value == nameof(ParticleSystemGradientMode.Color);
            bool twoColors = _gradientMode.value == nameof(ParticleSystemGradientMode.TwoColors);
            bool gradient = _gradientMode.value == nameof(ParticleSystemGradientMode.Gradient);
            bool twoGradients = _gradientMode.value == nameof(ParticleSystemGradientMode.TwoGradients);
            bool randomColor = _gradientMode.value == nameof(ParticleSystemGradientMode.RandomColor);
            _gradientColorMin.style.display = twoColors ? DisplayStyle.Flex : DisplayStyle.None;
            _gradientColorMax.style.display = color || twoColors ? DisplayStyle.Flex : DisplayStyle.None;
            _gradientMin.style.display = twoGradients ? DisplayStyle.Flex : DisplayStyle.None;
            _gradientMax.style.display = gradient || twoGradients || randomColor ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private ParticleSystem.MinMaxGradient ResolveSourceGradient()
        {
            VfxMinMaxGradientSnapshotView snapshot = (EditingMember()?.Snapshot ?? _members.FirstOrDefault()?.Snapshot)?.ColorOverLifetime;
            return VfxMinMaxGradientAuthoringJson.Restore(snapshot, new ParticleSystem.MinMaxGradient(UnityEngine.Color.white));
        }

        private static Gradient CloneGradient(Gradient source)
        {
            if (source == null) return null;
            var result = new Gradient { mode = source.mode };
            result.SetKeys(source.colorKeys, source.alphaKeys);
            return result;
        }

        private static Gradient WhiteGradient()
        {
            var result = new Gradient();
            result.SetKeys(
                new[] { new GradientColorKey(UnityEngine.Color.white, 0f), new GradientColorKey(UnityEngine.Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return result;
        }

        private ParticleSystem.MinMaxCurve ResolveSourceMinMaxCurve(string propertyId)
        {
            VfxParticleSystemSnapshotView snapshot = EditingMember()?.Snapshot ?? _members.FirstOrDefault()?.Snapshot;
            if (snapshot == null) return new ParticleSystem.MinMaxCurve(1f);
            VfxMinMaxCurveSnapshotView source = propertyId switch
            {
                "emission.rate.time" => snapshot.RateOverTime,
                "emission.rate.distance" => snapshot.RateOverDistance,
                "shape.particle.sizeEnvelope" => snapshot.SizeOverLifetimeX,
                "motion.velocity.envelope" => snapshot.VelocitySpeedModifier,
                "motion.noise.envelope" => snapshot.NoiseStrength,
                "timing.startDelay.exact" => snapshot.StartDelay,
                "timing.startLifetime.exact" => snapshot.StartLifetime,
                "motion.startSpeed.exact" => snapshot.StartSpeed,
                "shape.startSize.exact" => snapshot.StartSize,
                "shape.startSize.x.exact" => snapshot.StartSizeX,
                "shape.startSize.y.exact" => snapshot.StartSizeY,
                "shape.startSize.z.exact" => snapshot.StartSizeZ,
                "motion.startRotation.exact" => snapshot.StartRotation,
                "motion.startRotation.x.exact" => snapshot.StartRotationX,
                "motion.startRotation.y.exact" => snapshot.StartRotationY,
                "motion.startRotation.z.exact" => snapshot.StartRotationZ,
                "motion.force.x.exact" => snapshot.ForceOverLifetimeX,
                "motion.force.y.exact" => snapshot.ForceOverLifetimeY,
                "motion.force.z.exact" => snapshot.ForceOverLifetimeZ,
                "motion.rotation.x.exact" => snapshot.RotationOverLifetimeX,
                "motion.rotation.y.exact" => snapshot.RotationOverLifetimeY,
                "motion.rotation.z.exact" => snapshot.RotationOverLifetimeZ,
                "motion.noise.scroll.exact" => snapshot.NoiseScrollSpeed,
                "render.flipbook.frame.exact" => snapshot.TextureFrameOverTime,
                "render.flipbook.startFrame.exact" => snapshot.TextureStartFrame,
                "render.customData.custom1.x.exact" => CustomDataCurve(snapshot, ParticleSystemCustomData.Custom1, 0),
                "render.customData.custom1.y.exact" => CustomDataCurve(snapshot, ParticleSystemCustomData.Custom1, 1),
                "render.customData.custom1.z.exact" => CustomDataCurve(snapshot, ParticleSystemCustomData.Custom1, 2),
                "render.customData.custom1.w.exact" => CustomDataCurve(snapshot, ParticleSystemCustomData.Custom1, 3),
                "render.customData.custom2.x.exact" => CustomDataCurve(snapshot, ParticleSystemCustomData.Custom2, 0),
                "render.customData.custom2.y.exact" => CustomDataCurve(snapshot, ParticleSystemCustomData.Custom2, 1),
                "render.customData.custom2.z.exact" => CustomDataCurve(snapshot, ParticleSystemCustomData.Custom2, 2),
                "render.customData.custom2.w.exact" => CustomDataCurve(snapshot, ParticleSystemCustomData.Custom2, 3),
                _ => null
            };
            return VfxMinMaxCurveAuthoringJson.Restore(source, new ParticleSystem.MinMaxCurve(1f));
        }

        private static bool IsMinMaxCurveProperty(string propertyId)
        {
            return propertyId == "emission.rate.time"
                || propertyId == "emission.rate.distance"
                || propertyId == "shape.particle.sizeEnvelope"
                || propertyId == "motion.velocity.envelope"
                || propertyId == "motion.noise.envelope"
                || IsExactMinMaxOnlyProperty(propertyId);
        }

        private static bool IsExactMinMaxOnlyProperty(string propertyId)
        {
            return propertyId == "timing.startDelay.exact"
                || propertyId == "timing.startLifetime.exact"
                || propertyId == "motion.startSpeed.exact"
                || propertyId == "shape.startSize.exact"
                || propertyId == "shape.startSize.x.exact"
                || propertyId == "shape.startSize.y.exact"
                || propertyId == "shape.startSize.z.exact"
                || propertyId == "motion.startRotation.exact"
                || propertyId == "motion.startRotation.x.exact"
                || propertyId == "motion.startRotation.y.exact"
                || propertyId == "motion.startRotation.z.exact"
                || propertyId == "motion.force.x.exact"
                || propertyId == "motion.force.y.exact"
                || propertyId == "motion.force.z.exact"
                || propertyId == "motion.rotation.x.exact"
                || propertyId == "motion.rotation.y.exact"
                || propertyId == "motion.rotation.z.exact"
                || propertyId == "motion.noise.scroll.exact"
                || propertyId == "render.flipbook.frame.exact"
                || propertyId == "render.flipbook.startFrame.exact"
                || propertyId.StartsWith("render.customData.", StringComparison.Ordinal);
        }

        private static VfxMinMaxCurveSnapshotView CustomDataCurve(VfxParticleSystemSnapshotView snapshot, ParticleSystemCustomData stream, int channel)
        {
            VfxCustomDataStreamSnapshotView data = snapshot?.CustomData?.FirstOrDefault(value => value.Stream == (int)stream);
            return data?.Mode == (int)ParticleSystemCustomDataMode.Vector && data.Vector.Count > channel ? data.Vector[channel] : null;
        }

        private void BindEmissionWindow(VfxCompositionLayerView layer)
        {
            VfxParticleSystemSnapshotView snapshot = EditingMember()?.Snapshot ?? _members.FirstOrDefault()?.Snapshot;
            int sourceStart = snapshot == null ? 0 : Mathf.Max(0, Mathf.RoundToInt(VfxMinMaxCurveAuthoringJson.Restore(snapshot.StartDelay, new ParticleSystem.MinMaxCurve(0f)).constantMax * 60f));
            int sourceDuration = snapshot == null ? Math.Max(1, layer?.EstimatedSourceFrames ?? 1) : Math.Max(1, Mathf.RoundToInt(snapshot.DurationSeconds * 60f));
            _binding = true;
            try
            {
                _emissionWindowStart.SetValueWithoutNotify(Int(layer, "emission.window.startFrame", sourceStart));
                _emissionWindowEnd.SetValueWithoutNotify(Int(layer, "emission.window.endFrame", sourceStart + sourceDuration));
                _flipbookCycles.SetValueWithoutNotify(Int(layer, "render.flipbook.cycles", snapshot?.TextureCycleCount ?? 0));
            }
            finally { _binding = false; }
        }

        private AnimationCurve ResolveSourceCurveForEditor()
        {
            if (TryResolveSourceCurve(out AnimationCurve curve, out _)) return curve;
            string propertyId = CurvePropertyId(_curveProperty.value);
            if (IsExactMinMaxOnlyProperty(propertyId))
            {
                ParticleSystem.MinMaxCurve exact = ResolveSourceMinMaxCurve(propertyId);
                if (exact.mode == ParticleSystemCurveMode.Curve || exact.mode == ParticleSystemCurveMode.TwoCurves)
                    return VfxCurveAuthoringJson.Clone(exact.curveMax) ?? AnimationCurve.Constant(0f, 1f, exact.constantMax);
                return AnimationCurve.Constant(0f, 1f, exact.mode == ParticleSystemCurveMode.TwoConstants ? exact.constantMax : exact.constant);
            }
            if (_curveProperty.value == "透明度包络")
            {
                VfxTimeSegmentsView segments = _compiled?.SourceSegments ?? _selectedLayer()?.Timing?.SourceSegments;
                if (segments != null)
                {
                    int total = Math.Max(1, segments.ArtisticFrames);
                    float pre = segments.PreRollFrames / (float)total;
                    float entry = (segments.PreRollFrames + segments.EntryFrames) / (float)total;
                    float bodyEnd = (segments.PreRollFrames + segments.EntryFrames + segments.BodyFrames) / (float)total;
                    return new AnimationCurve(
                        new Keyframe(0f, 0f), new Keyframe(pre, 0f), new Keyframe(entry, 1f),
                        new Keyframe(bodyEnd, 1f), new Keyframe(1f, 0f));
                }
            }
            return AnimationCurve.Linear(0f, 1f, 1f, 1f);
        }

        private static AnimationCurve SnapshotCurve(VfxMinMaxCurveSnapshotView value)
        {
            if (value == null) return null;
            ParticleSystemCurveMode mode = (ParticleSystemCurveMode)value.Mode;
            if (mode == ParticleSystemCurveMode.Curve || mode == ParticleSystemCurveMode.TwoCurves)
            {
                VfxCurveSnapshotView source = value.CurveMax ?? value.CurveMin;
                if (source?.Keys == null || source.Keys.Count == 0) return null;
                var keys = new Keyframe[source.Keys.Count];
                for (int i = 0; i < keys.Length; i++)
                {
                    VfxCurveKeySnapshotView key = source.Keys[i];
                    keys[i] = new Keyframe(key.Time, key.Value * value.CurveMultiplier, key.ScaleInTangent(value.CurveMultiplier), key.ScaleOutTangent(value.CurveMultiplier), key.InWeight, key.OutWeight)
                    {
                        weightedMode = (WeightedMode)key.WeightedMode
                    };
                }
                return new AnimationCurve(keys) { preWrapMode = (WrapMode)source.PreWrapMode, postWrapMode = (WrapMode)source.PostWrapMode };
            }
            float constant = mode == ParticleSystemCurveMode.TwoConstants ? value.ConstantMax : value.ConstantMax;
            return AnimationCurve.Constant(0f, 1f, constant);
        }

        private static AnimationCurve ScaleCurve(AnimationCurve source, float factor)
        {
            if (source == null) return null;
            Keyframe[] keys = source.keys;
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i].value *= factor;
                keys[i].inTangent *= factor;
                keys[i].outTangent *= factor;
            }
            return new AnimationCurve(keys) { preWrapMode = source.preWrapMode, postWrapMode = source.postWrapMode };
        }

        private static float CurveMaximum(params AnimationCurve[] curves)
        {
            float maximum = 0.0001f;
            for (int curveIndex = 0; curveIndex < curves.Length; curveIndex++)
            {
                AnimationCurve curve = curves[curveIndex];
                if (curve == null) continue;
                for (int sample = 0; sample <= 64; sample++)
                    maximum = Mathf.Max(maximum, Mathf.Abs(curve.Evaluate(sample / 64f)));
            }
            return maximum;
        }

        private static void DrawAnimationCurve(Rect rect, AnimationCurve curve, float maximum, Color color, float width)
        {
            if (curve == null) return;
            var points = new Vector3[65];
            for (int i = 0; i < points.Length; i++)
            {
                float x = i / 64f;
                float y = Mathf.Clamp01(curve.Evaluate(x) / Mathf.Max(0.0001f, maximum));
                points[i] = Point(rect, x, y);
            }
            Handles.color = color;
            Handles.DrawAAPolyLine(width, points);
        }

        private static void DrawGrid(Rect rect)
        {
            Color line = new Color(0.20f, 0.27f, 0.32f, 0.6f);
            for (int i = 1; i < 4; i++) EditorGUI.DrawRect(new Rect(rect.x, rect.y + rect.height * i / 4f, rect.width, 1f), line);
            for (int i = 1; i < 6; i++) EditorGUI.DrawRect(new Rect(rect.x + rect.width * i / 6f, rect.y, 1f, rect.height), line);
        }

        private static void DrawEnvelope(Rect rect, VfxTimeSegmentsView segments, Color color, float width)
        {
            int total = Math.Max(1, segments.ArtisticFrames);
            float pre = segments.PreRollFrames / (float)total;
            float entry = (segments.PreRollFrames + segments.EntryFrames) / (float)total;
            float body = (segments.PreRollFrames + segments.EntryFrames + segments.BodyFrames) / (float)total;
            Vector3[] points =
            {
                Point(rect, 0f, 0f), Point(rect, pre, 0f), Point(rect, entry, 1f),
                Point(rect, body, 1f), Point(rect, 1f, 0f)
            };
            Handles.color = color;
            Handles.DrawAAPolyLine(width, points);
        }

        private static void DrawBodyDelta(Rect rect, VfxTimeSegmentsView source, VfxTimeSegmentsView target, Color color, float width)
        {
            if (source.BodyFrames == target.BodyFrames) return;
            float start = (target.PreRollFrames + target.EntryFrames) / (float)Math.Max(1, target.ArtisticFrames);
            float end = (target.PreRollFrames + target.EntryFrames + target.BodyFrames) / (float)Math.Max(1, target.ArtisticFrames);
            Handles.color = color;
            Handles.DrawAAPolyLine(width, Point(rect, start, 0.92f), Point(rect, end, 0.92f));
        }

        private static Vector3 Point(Rect rect, float normalizedX, float normalizedY)
        {
            return new Vector3(rect.x + normalizedX * rect.width, rect.yMax - normalizedY * rect.height, 0f);
        }

        private static string ResolveCurveDomain(string display)
        {
            return display switch
            {
                "透明度包络" => "Opacity · Layer Frame（灰=源 / 蓝=修改 / 白=最终）",
                "发射包络" => "Emission · Emitter 0–100%",
                "距离发射包络" => "Emission Distance · Emitter 0–100%",
                "粒子尺寸" => "Size · Particle Lifetime 0–100%",
                "运动速度" => "Velocity · Particle Lifetime 0–100%",
                "Noise 强度" => "Noise · Profile 指定时间域",
                "溶解" => "Dissolve · Layer Frame",
                "侵蚀" => "Erosion · Layer Frame",
                "材质局部时间" => "Material Local Time · Layer Frame",
                "起始延迟（精确）" => "Main.startDelay · Layer Frame / Seconds",
                "粒子生命周期（精确）" => "Main.startLifetime · Particle Residence / Seconds",
                "初始速度（精确）" => "Main.startSpeed · Spawn / m·s⁻¹",
                "初始尺寸（精确）" => "Main.startSize · Spawn / Meter",
                "初始尺寸 X（精确）" => "Main.startSizeX · Spawn / Meter",
                "初始尺寸 Y（精确）" => "Main.startSizeY · Spawn / Meter",
                "初始尺寸 Z（精确）" => "Main.startSizeZ · Spawn / Meter",
                "初始旋转（精确）" => "Main.startRotation · Spawn / Radian",
                "初始旋转 X（精确）" => "Main.startRotationX · Spawn / Radian",
                "初始旋转 Y（精确）" => "Main.startRotationY · Spawn / Radian",
                "初始旋转 Z（精确）" => "Main.startRotationZ · Spawn / Radian",
                "Noise 滚动（精确）" => "Noise.scrollSpeed · Particle Lifetime 0–100%",
                "Flipbook 帧（精确）" => "TextureSheet.frameOverTime · Particle Lifetime 0–100%",
                "Flipbook 起始帧（精确）" => "TextureSheet.startFrame · Spawn",
                "Custom1 X（精确）" => "Custom1.X · Shader Profile / Particle Lifetime",
                "Custom1 Y（精确）" => "Custom1.Y · Shader Profile / Particle Lifetime",
                "Custom1 Z（精确）" => "Custom1.Z · Shader Profile / Particle Lifetime",
                "Custom1 W（精确）" => "Custom1.W · Shader Profile / Particle Lifetime",
                "Custom2 X（精确）" => "Custom2.X · Shader Profile / Particle Lifetime",
                "Custom2 Y（精确）" => "Custom2.Y · Shader Profile / Particle Lifetime",
                "Custom2 Z（精确）" => "Custom2.Z · Shader Profile / Particle Lifetime",
                "Custom2 W（精确）" => "Custom2.W · Shader Profile / Particle Lifetime",
                _ => "Particle Lifetime 0–100% · Exact MinMaxCurve"
            };
        }

        private static string CurvePropertyId(string display)
        {
            return display switch
            {
                "透明度包络" => "appearance.opacityEnvelope",
                "发射包络" => "emission.rate.time",
                "距离发射包络" => "emission.rate.distance",
                "粒子尺寸" => "shape.particle.sizeEnvelope",
                "运动速度" => "motion.velocity.envelope",
                "Noise 强度" => "motion.noise.envelope",
                "溶解" => "appearance.dissolve",
                "侵蚀" => "appearance.erosion",
                "材质局部时间" => "timing.materialLocalTime.curve",
                "起始延迟（精确）" => "timing.startDelay.exact",
                "粒子生命周期（精确）" => "timing.startLifetime.exact",
                "初始速度（精确）" => "motion.startSpeed.exact",
                "初始尺寸（精确）" => "shape.startSize.exact",
                "初始尺寸 X（精确）" => "shape.startSize.x.exact",
                "初始尺寸 Y（精确）" => "shape.startSize.y.exact",
                "初始尺寸 Z（精确）" => "shape.startSize.z.exact",
                "初始旋转（精确）" => "motion.startRotation.exact",
                "初始旋转 X（精确）" => "motion.startRotation.x.exact",
                "初始旋转 Y（精确）" => "motion.startRotation.y.exact",
                "初始旋转 Z（精确）" => "motion.startRotation.z.exact",
                "持续受力 X（精确）" => "motion.force.x.exact",
                "持续受力 Y（精确）" => "motion.force.y.exact",
                "持续受力 Z（精确）" => "motion.force.z.exact",
                "旋转速度 X（精确）" => "motion.rotation.x.exact",
                "旋转速度 Y（精确）" => "motion.rotation.y.exact",
                "旋转速度 Z（精确）" => "motion.rotation.z.exact",
                "Noise 滚动（精确）" => "motion.noise.scroll.exact",
                "Flipbook 帧（精确）" => "render.flipbook.frame.exact",
                "Flipbook 起始帧（精确）" => "render.flipbook.startFrame.exact",
                "Custom1 X（精确）" => "render.customData.custom1.x.exact",
                "Custom1 Y（精确）" => "render.customData.custom1.y.exact",
                "Custom1 Z（精确）" => "render.customData.custom1.z.exact",
                "Custom1 W（精确）" => "render.customData.custom1.w.exact",
                "Custom2 X（精确）" => "render.customData.custom2.x.exact",
                "Custom2 Y（精确）" => "render.customData.custom2.y.exact",
                "Custom2 Z（精确）" => "render.customData.custom2.z.exact",
                "Custom2 W（精确）" => "render.customData.custom2.w.exact",
                _ => "timing.materialLocalTime.curve"
            };
        }

        private static VfxSemanticOperation ParseOperation(string value)
        {
            return Enum.TryParse(value, false, out VfxSemanticOperation operation) ? operation : VfxSemanticOperation.Multiply;
        }

        private float Float(VfxCompositionLayerView layer, string propertyId, float fallback)
        {
            return Find(layer, propertyId)?.FloatValue(fallback) ?? fallback;
        }

        private int Int(VfxCompositionLayerView layer, string propertyId, int fallback)
        {
            return Find(layer, propertyId)?.IntValue(fallback) ?? fallback;
        }

        private bool Bool(VfxCompositionLayerView layer, string propertyId, bool fallback)
        {
            VfxSemanticOverrideView value = Find(layer, propertyId);
            return value?.Value?.Type == JTokenType.Boolean ? value.Value.Value<bool>() : fallback;
        }

        private Color Color(VfxCompositionLayerView layer, string propertyId, Color fallback)
        {
            VfxSemanticOverrideView value = Find(layer, propertyId);
            if (value?.Value is not JArray array || array.Count < 3) return fallback;
            return new Color(array[0].Value<float>(), array[1].Value<float>(), array[2].Value<float>(), array.Count > 3 ? array[3].Value<float>() : 1f);
        }

        private Vector3 Vector(VfxCompositionLayerView layer, string propertyId, Vector3 fallback)
        {
            VfxSemanticOverrideView value = Find(layer, propertyId);
            if (value?.Value is not JArray array || array.Count < 3) return fallback;
            return new Vector3(array[0].Value<float>(), array[1].Value<float>(), array[2].Value<float>());
        }

        private void SetRendererFields(VfxCompositionLayerView layer)
        {
            VfxParticleSystemSnapshotView source = EditingMember()?.Snapshot ?? _members.FirstOrDefault()?.Snapshot;
            int mode = Int(layer, "render.renderer.mode", source?.RendererMode ?? (int)ParticleSystemRenderMode.Billboard);
            int alignment = Int(layer, "render.renderer.alignment", source?.RendererAlignment ?? (int)ParticleSystemRenderSpace.View);
            int sortMode = Int(layer, "render.renderer.sortMode", source?.RendererSortMode ?? (int)ParticleSystemSortMode.None);
            _binding = true;
            try
            {
                _renderMode.SetValueWithoutNotify(Enum.IsDefined(typeof(ParticleSystemRenderMode), mode) ? ((ParticleSystemRenderMode)mode).ToString() : nameof(ParticleSystemRenderMode.Billboard));
                _renderAlignment.SetValueWithoutNotify(Enum.IsDefined(typeof(ParticleSystemRenderSpace), alignment) ? ((ParticleSystemRenderSpace)alignment).ToString() : nameof(ParticleSystemRenderSpace.View));
                _renderSortMode.SetValueWithoutNotify(Enum.IsDefined(typeof(ParticleSystemSortMode), sortMode) ? ((ParticleSystemSortMode)sortMode).ToString() : nameof(ParticleSystemSortMode.None));
                _renderPivot.SetValueWithoutNotify(Vector(layer, "render.renderer.pivot", SnapshotVector(source?.RendererPivot, Vector3.zero)));
                _renderMinSize.SetValueWithoutNotify(Float(layer, "render.renderer.minParticleSize", source?.RendererMinParticleSize ?? 0f));
                _renderMaxSize.SetValueWithoutNotify(Float(layer, "render.renderer.maxParticleSize", source?.RendererMaxParticleSize ?? 0.5f));
                _renderMaterial.SetValueWithoutNotify(AssetFromGuid<Material>(AssetReference(layer, "render.renderer.material", source?.MaterialGuids?.FirstOrDefault())));
                _renderMesh.SetValueWithoutNotify(AssetFromGuid<Mesh>(AssetReference(layer, "render.renderer.mesh", source?.MeshGuid)));
                // Resolved members are captured from a temporary preview instance that is
                // destroyed after compilation. Renderer defaults must therefore come from
                // the durable snapshot instead of retaining a Unity component reference.
                Material sourceMaterial = AssetFromGuid<Material>(source?.MaterialGuids?.FirstOrDefault());
                int sourceQueue = sourceMaterial?.renderQueue ?? 0;
                int sourceCull = sourceMaterial != null && sourceMaterial.HasProperty("_Cull") ? sourceMaterial.GetInt("_Cull") : (int)CullMode.Off;
                int sourceZWrite = sourceMaterial != null && sourceMaterial.HasProperty("_ZWrite") ? sourceMaterial.GetInt("_ZWrite") : 0;
                int sourceSrcBlend = sourceMaterial != null && sourceMaterial.HasProperty("_SrcBlend") ? sourceMaterial.GetInt("_SrcBlend") : (int)BlendMode.One;
                int sourceDstBlend = sourceMaterial != null && sourceMaterial.HasProperty("_DstBlend") ? sourceMaterial.GetInt("_DstBlend") : (int)BlendMode.Zero;
                int cull = Int(layer, "render.state.cull", sourceCull);
                int srcBlend = Int(layer, "render.state.srcBlend", sourceSrcBlend);
                int dstBlend = Int(layer, "render.state.dstBlend", sourceDstBlend);
                _renderQueue.SetValueWithoutNotify(Int(layer, "render.state.renderQueue", sourceQueue));
                _renderCull.SetValueWithoutNotify(Enum.IsDefined(typeof(CullMode), cull) ? ((CullMode)cull).ToString() : nameof(CullMode.Off));
                _renderZWrite.SetValueWithoutNotify(Bool(layer, "render.state.zWrite", sourceZWrite != 0));
                _renderSrcBlend.SetValueWithoutNotify(Enum.IsDefined(typeof(BlendMode), srcBlend) ? ((BlendMode)srcBlend).ToString() : nameof(BlendMode.One));
                _renderDstBlend.SetValueWithoutNotify(Enum.IsDefined(typeof(BlendMode), dstBlend) ? ((BlendMode)dstBlend).ToString() : nameof(BlendMode.Zero));
                BindTextureField(_textureMain, layer, "render.texture.main", sourceMaterial);
                BindTextureField(_textureMask, layer, "render.texture.mask", sourceMaterial);
                BindTextureField(_textureNoise, layer, "render.texture.noise", sourceMaterial);
                BindTextureField(_textureRamp, layer, "render.texture.ramp", sourceMaterial);
                BindTextureField(_textureNormal, layer, "render.texture.normal", sourceMaterial);
                BindTextureField(_textureFlipbook, layer, "render.texture.flipbook", sourceMaterial);
                int editableTextureSlots = TextureFields().Count(value => value.Field.enabledSelf);
                _textureContract.text = editableTextureSlots > 0
                    ? "当前 Shader Profile 提供 " + editableTextureSlots + " 个可编辑贴图槽；修改会创建可追踪派生材质。"
                    : "当前 Shader Profile 没有已确认的贴图槽绑定；请先在成员页确认共享 Shader Profile。";
            }
            finally { _binding = false; }
        }

        private void RefreshEditScopeChoices()
        {
            if (_editScope == null) return;
            var choices = new List<string> { "Layer 主控" };
            _editScopeMemberIds.Clear();
            _editScopeMemberIds.Add(string.Empty);
            var duplicateNames = _members
                .GroupBy(value => value.Source?.DisplayName ?? value.MemberId, StringComparer.Ordinal)
                .ToDictionary(value => value.Key, value => value.Count(), StringComparer.Ordinal);
            for (int i = 0; i < _members.Count; i++)
            {
                VfxResolvedComponentAuthoringProfile member = _members[i];
                string name = member.Source?.DisplayName ?? member.MemberId;
                if (duplicateNames.TryGetValue(name, out int count) && count > 1)
                    name += " · " + ShortId(member.MemberId);
                choices.Add("成员 · " + name);
                _editScopeMemberIds.Add(member.MemberId);
            }
            int selectedIndex = string.IsNullOrWhiteSpace(_editMemberId)
                ? 0
                : _editScopeMemberIds.FindIndex(value => string.Equals(value, _editMemberId, StringComparison.Ordinal));
            if (selectedIndex < 0)
            {
                selectedIndex = 0;
                _editMemberId = string.Empty;
            }
            _binding = true;
            try
            {
                _editScope.choices = choices;
                _editScope.SetValueWithoutNotify(choices[selectedIndex]);
            }
            finally { _binding = false; }
        }

        private void UpdateEditScopeSummary(VfxCompositionLayerView layer)
        {
            if (_editScopeSummary == null) return;
            if (string.IsNullOrWhiteSpace(_editMemberId))
            {
                _editScopeSummary.text = "Layer 主控：所有处于“跟随”的成员继承这里的属性值；时间分段始终属于 Layer 合同。";
                _resetSource.text = "恢复整层源值";
                return;
            }
            VfxResolvedComponentAuthoringProfile member = EditingMember();
            int overrideCount = layer?.MemberOverrides?
                .FirstOrDefault(value => string.Equals(value.MemberId, _editMemberId, StringComparison.Ordinal))?
                .Overrides?.Count ?? 0;
            string name = member?.Source?.DisplayName ?? _editMemberId;
            _editScopeSummary.text = "成员独立值：" + name + " · " + overrideCount
                + " 项覆盖；未设置的属性继承 Layer 主控，时间分段仍编辑 Layer 合同。";
            _resetSource.text = "清除本成员覆盖";
        }

        private VfxResolvedComponentAuthoringProfile EditingMember()
        {
            return string.IsNullOrWhiteSpace(_editMemberId)
                ? null
                : _members.FirstOrDefault(value => string.Equals(value.MemberId, _editMemberId, StringComparison.Ordinal));
        }

        private VfxSemanticOverrideView Find(VfxCompositionLayerView layer, string propertyId)
        {
            if (layer == null) return null;
            if (!string.IsNullOrWhiteSpace(_editMemberId))
            {
                VfxSemanticOverrideView memberValue = layer.MemberOverrides?
                    .FirstOrDefault(value => string.Equals(value.MemberId, _editMemberId, StringComparison.Ordinal))?
                    .Overrides?
                    .LastOrDefault(value => value.PropertyId == propertyId);
                if (memberValue != null) return memberValue;
            }
            return layer.SemanticOverrides?.LastOrDefault(value => value.PropertyId == propertyId);
        }

        private List<VfxSemanticOverrideView> EditingOverrides(VfxCompositionLayerView layer, bool create)
        {
            layer.SemanticOverrides ??= new List<VfxSemanticOverrideView>();
            if (string.IsNullOrWhiteSpace(_editMemberId)) return layer.SemanticOverrides;
            layer.MemberOverrides ??= new List<VfxMemberSemanticOverrideView>();
            VfxMemberSemanticOverrideView member = layer.MemberOverrides.FirstOrDefault(value =>
                string.Equals(value.MemberId, _editMemberId, StringComparison.Ordinal));
            if (member == null && create)
            {
                member = new VfxMemberSemanticOverrideView { MemberId = _editMemberId };
                layer.MemberOverrides.Add(member);
            }
            if (member == null) return null;
            member.Overrides ??= new List<VfxSemanticOverrideView>();
            return member.Overrides;
        }

        private static int LayerInt(VfxCompositionLayerView layer, string propertyId, int fallback)
        {
            return layer.SemanticOverrides?.LastOrDefault(value => value.PropertyId == propertyId)?.IntValue(fallback) ?? fallback;
        }

        private static void UpsertLayerInt(
            VfxCompositionLayerView layer,
            string propertyId,
            VfxSemanticOperation operation,
            int value,
            int neutral)
        {
            if (value == neutral)
            {
                layer.SemanticOverrides.RemoveAll(item => item.PropertyId == propertyId);
                return;
            }
            VfxSemanticOverrideView existing = layer.SemanticOverrides.FirstOrDefault(item => item.PropertyId == propertyId);
            if (existing == null)
            {
                existing = new VfxSemanticOverrideView { PropertyId = propertyId };
                layer.SemanticOverrides.Add(existing);
            }
            existing.Operation = operation.ToString();
            existing.Value = new JValue(value);
        }

        private static string ShortId(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "—";
            int separator = value.LastIndexOfAny(new[] { '/', '.', ':' });
            string tail = separator >= 0 && separator + 1 < value.Length ? value.Substring(separator + 1) : value;
            return tail.Length <= 12 ? tail : tail.Substring(tail.Length - 12);
        }

        private void ConfigureTextureField(ObjectField field, string propertyId)
        {
            if (field == null) return;
            field.objectType = typeof(Texture);
            field.RegisterValueChangedCallback(evt => Change(layer => UpsertAsset(layer, propertyId, evt.newValue)));
        }

        private void BindTextureField(
            ObjectField field,
            VfxCompositionLayerView layer,
            string propertyId,
            Material sourceMaterial)
        {
            if (field == null) return;
            IEnumerable<VfxResolvedComponentAuthoringProfile> bindingMembers = EditingMember() is { } editingMember
                ? new[] { editingMember }
                : _members;
            VfxShaderPropertyBindingView binding = bindingMembers
                .SelectMany(value => value.ShaderProfile?.Bindings ?? Enumerable.Empty<VfxShaderPropertyBindingView>())
                .FirstOrDefault(value =>
                    value.SemanticPropertyId == propertyId
                    && value.RequiresDerivedMaterial
                    && string.Equals(value.PropertyType, "Texture", StringComparison.Ordinal));
            bool editable = binding != null;
            string fallbackGuid = string.Empty;
            if (editable && sourceMaterial != null && sourceMaterial.HasProperty(binding.ShaderPropertyName))
            {
                Texture texture = sourceMaterial.GetTexture(binding.ShaderPropertyName);
                fallbackGuid = texture == null
                    ? string.Empty
                    : AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(texture));
            }
            field.SetEnabled(editable);
            field.tooltip = editable
                ? propertyId + " → " + binding.ShaderPropertyName + "（派生材质）"
                : propertyId + "：当前 Shader Profile 未声明安全槽位";
            field.SetValueWithoutNotify(AssetFromGuid<Texture>(AssetReference(layer, propertyId, fallbackGuid)));
        }

        private IEnumerable<(ObjectField Field, string PropertyId)> TextureFields()
        {
            yield return (_textureMain, "render.texture.main");
            yield return (_textureMask, "render.texture.mask");
            yield return (_textureNoise, "render.texture.noise");
            yield return (_textureRamp, "render.texture.ramp");
            yield return (_textureNormal, "render.texture.normal");
            yield return (_textureFlipbook, "render.texture.flipbook");
        }

        private static Vector3 SnapshotVector(List<float> value, Vector3 fallback)
        {
            return value != null && value.Count >= 3 ? new Vector3(value[0], value[1], value[2]) : fallback;
        }

        private string AssetReference(VfxCompositionLayerView layer, string propertyId, string fallback)
        {
            VfxSemanticOverrideView value = Find(layer, propertyId);
            return value?.Value?.Type == JTokenType.String ? value.Value.Value<string>() : fallback ?? string.Empty;
        }

        private static T AssetFromGuid<T>(string guid) where T : UnityEngine.Object
        {
            return string.IsNullOrWhiteSpace(guid) ? null : AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
        }

        private void UpsertFloat(VfxCompositionLayerView layer, string propertyId, VfxSemanticOperation operation, float value, float neutral)
        {
            if (Mathf.Abs(value - neutral) <= 0.00001f) { Remove(layer, propertyId); return; }
            Upsert(layer, propertyId, operation, new JValue(value));
        }

        private void UpsertInt(VfxCompositionLayerView layer, string propertyId, VfxSemanticOperation operation, int value, int neutral)
        {
            if (value == neutral) { Remove(layer, propertyId); return; }
            Upsert(layer, propertyId, operation, new JValue(value));
        }

        private void UpsertBool(VfxCompositionLayerView layer, string propertyId, bool value)
        {
            Upsert(layer, propertyId, VfxSemanticOperation.Replace, new JValue(value));
        }

        private void UpsertColor(VfxCompositionLayerView layer, string propertyId, Color value)
        {
            if (ApproximatelyWhite(value)) { Remove(layer, propertyId); return; }
            Upsert(layer, propertyId, VfxSemanticOperation.Multiply, new JArray(value.r, value.g, value.b, value.a, 0f));
        }

        private void UpsertCurve(VfxCompositionLayerView layer, string propertyId, VfxSemanticOperation operation, AnimationCurve value)
        {
            if (value == null || value.length == 0) { Remove(layer, propertyId); return; }
            Upsert(layer, propertyId, operation, VfxCurveAuthoringJson.Capture(value));
        }

        private void UpsertVector(VfxCompositionLayerView layer, string propertyId, VfxSemanticOperation operation, Vector3 value)
        {
            if (value.sqrMagnitude <= 0.00000001f) { Remove(layer, propertyId); return; }
            Upsert(layer, propertyId, operation, new JArray(value.x, value.y, value.z));
        }

        private void UpsertVectorExact(VfxCompositionLayerView layer, string propertyId, Vector3 value)
        {
            Upsert(layer, propertyId, VfxSemanticOperation.Replace, new JArray(value.x, value.y, value.z));
        }

        private void UpsertAsset(VfxCompositionLayerView layer, string propertyId, UnityEngine.Object value)
        {
            if (value == null)
            {
                Remove(layer, propertyId);
                return;
            }
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(value));
            if (string.IsNullOrWhiteSpace(guid)) return;
            Upsert(layer, propertyId, VfxSemanticOperation.Replace, new JValue(guid));
        }

        private void Upsert(VfxCompositionLayerView layer, string propertyId, VfxSemanticOperation operation, JToken value)
        {
            List<VfxSemanticOverrideView> overrides = EditingOverrides(layer, create: true);
            VfxSemanticOverrideView existing = overrides.FirstOrDefault(item => item.PropertyId == propertyId);
            if (existing == null)
            {
                existing = new VfxSemanticOverrideView { PropertyId = propertyId };
                overrides.Add(existing);
            }
            existing.Operation = operation.ToString();
            existing.Value = value;
            if (!string.IsNullOrWhiteSpace(_editMemberId))
                SetPropertyParticipation(layer, _editMemberId, propertyId, VfxMemberParticipationState.Override);
            VfxSemanticPropertyDefinitionView definition = new VfxSemanticPropertyRegistryService().Load(out _)?.Find(propertyId);
            if (definition != null && (value.Type == JTokenType.Float || value.Type == JTokenType.Integer))
            {
                double number = value.Value<double>();
                existing.Experimental = (definition.RecommendedMinimum.HasValue && number < definition.RecommendedMinimum.Value)
                    || (definition.RecommendedMaximum.HasValue && number > definition.RecommendedMaximum.Value);
            }
        }

        private void Remove(VfxCompositionLayerView layer, string propertyId)
        {
            if (string.IsNullOrWhiteSpace(_editMemberId))
            {
                layer.SemanticOverrides.RemoveAll(value => value.PropertyId == propertyId);
                return;
            }
            VfxMemberSemanticOverrideView member = layer.MemberOverrides?.FirstOrDefault(value =>
                string.Equals(value.MemberId, _editMemberId, StringComparison.Ordinal));
            if (member == null) return;
            member.Overrides?.RemoveAll(value => value.PropertyId == propertyId);
            SetPropertyParticipation(layer, _editMemberId, propertyId, VfxMemberParticipationState.Follow);
            if ((member.Overrides == null || member.Overrides.Count == 0) && string.IsNullOrWhiteSpace(member.RetimeMode))
                layer.MemberOverrides.Remove(member);
        }

        private static bool ApproximatelyWhite(Color value)
        {
            return Mathf.Abs(value.r - 1f) < 0.00001f && Mathf.Abs(value.g - 1f) < 0.00001f && Mathf.Abs(value.b - 1f) < 0.00001f && Mathf.Abs(value.a - 1f) < 0.00001f;
        }

        private static void SetParticipation(VfxCompositionLayerView layer, string memberId, VfxMemberParticipationState state)
        {
            VfxMemberParticipationView existing = layer.MemberParticipation.FirstOrDefault(value => value.MemberId == memberId);
            if (state == VfxMemberParticipationState.Follow)
            {
                if (existing != null) layer.MemberParticipation.Remove(existing);
                return;
            }
            if (existing == null)
            {
                existing = new VfxMemberParticipationView { MemberId = memberId };
                layer.MemberParticipation.Add(existing);
            }
            existing.State = state.ToString();
        }

        private static void SetMemberRetimeMode(VfxCompositionLayerView layer, string memberId, string retimeMode)
        {
            VfxMemberSemanticOverrideView existing = layer.MemberOverrides.FirstOrDefault(value => value.MemberId == memberId);
            if (string.IsNullOrWhiteSpace(retimeMode))
            {
                if (existing == null) return;
                existing.RetimeMode = null;
                if (existing.Overrides == null || existing.Overrides.Count == 0) layer.MemberOverrides.Remove(existing);
                return;
            }
            if (existing == null)
            {
                existing = new VfxMemberSemanticOverrideView { MemberId = memberId };
                layer.MemberOverrides.Add(existing);
            }
            existing.RetimeMode = retimeMode;
        }

        private static void SetPropertyParticipation(VfxCompositionLayerView layer, string memberId, string propertyId, VfxMemberParticipationState state)
        {
            VfxMemberParticipationView existing = layer.MemberParticipation.FirstOrDefault(value => value.MemberId == memberId);
            if (existing == null)
            {
                if (state == VfxMemberParticipationState.Follow) return;
                existing = new VfxMemberParticipationView { MemberId = memberId };
                layer.MemberParticipation.Add(existing);
            }
            existing.PropertyStates ??= new Dictionary<string, string>(StringComparer.Ordinal);
            if (state == VfxMemberParticipationState.Follow) existing.PropertyStates.Remove(propertyId);
            else existing.PropertyStates[propertyId] = state.ToString();
            if (existing.State == nameof(VfxMemberParticipationState.Follow) && existing.PropertyStates.Count == 0)
                layer.MemberParticipation.Remove(existing);
        }

        private static VfxMemberParticipationState GetParticipation(VfxCompositionLayerView layer, VfxElementProfileView elementProfile, string memberId, string propertyId = null)
        {
            VfxMemberParticipationView value = layer?.MemberParticipation?.FirstOrDefault(item => item.MemberId == memberId);
            if (!string.IsNullOrWhiteSpace(propertyId)
                && value?.PropertyStates != null
                && value.PropertyStates.TryGetValue(propertyId, out string draftState)
                && Enum.TryParse(draftState, out VfxMemberParticipationState parsedDraft)) return parsedDraft;
            if (!string.IsNullOrWhiteSpace(propertyId))
            {
                VfxElementProfileMemberView profileMember = elementProfile?.Members?.FirstOrDefault(item => item.MemberId == memberId);
                if (profileMember?.Participation != null
                    && profileMember.Participation.TryGetValue(propertyId, out string profileState)
                    && Enum.TryParse(profileState, out VfxMemberParticipationState parsedProfile)) return parsedProfile;
            }
            return value != null && Enum.TryParse(value.State, out VfxMemberParticipationState state) ? state : VfxMemberParticipationState.Follow;
        }

        private static string ParticipationLabel(VfxMemberParticipationState state) => ParticipationChoices[(int)state];
        private static VfxMemberParticipationState ParseParticipation(string label)
        {
            int index = Array.IndexOf(ParticipationChoices, label);
            return index >= 0 ? (VfxMemberParticipationState)index : VfxMemberParticipationState.Follow;
        }

        private static string Curve(VfxMinMaxCurveSnapshotView value)
        {
            return value == null ? "—" : ((ParticleSystemCurveMode)value.Mode) + " × " + value.CurveMultiplier.ToString("0.###") + " [" + value.ConstantMin.ToString("0.###") + ", " + value.ConstantMax.ToString("0.###") + "]";
        }

        private sealed class VfxRawFieldRow
        {
            public readonly string Path;
            public readonly string Value;
            public VfxRawFieldRow(string path, string value) { Path = path; Value = value; }
        }
    }
}

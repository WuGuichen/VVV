using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal enum CharacterTestRuntimeAiPlannerFlowPreviewMode
    {
        Overview,
        Detail
    }

    internal sealed partial class CharacterTestRuntimeAiPlannerFlowPreview : VisualElement
    {
        public const string RootName = "runtime-ai-planner-flow-preview";
        public const string StateNodeClassName = "runtime-ai-planner-flow-state-node";
        public const string TransitionEdgeClassName = "runtime-ai-planner-flow-transition-edge";
        public const string GlobalNodeName = "runtime-ai-planner-flow-global";
        public const string MissingTargetNodeName = "runtime-ai-planner-flow-missing-target";
        public const string ViewportName = "runtime-ai-planner-flow-viewport";
        public const string ScrollName = "runtime-ai-planner-flow-scroll";
        public const string CanvasName = "runtime-ai-planner-flow-canvas";
        public const string ZoomOutButtonName = "runtime-ai-planner-flow-zoom-out";
        public const string ZoomInButtonName = "runtime-ai-planner-flow-zoom-in";
        public const string ResetZoomButtonName = "runtime-ai-planner-flow-zoom-reset";
        public const string FitZoomButtonName = "runtime-ai-planner-flow-zoom-fit";
        public const string CenterButtonName = "runtime-ai-planner-flow-center";
        public const string ExpandButtonName = "runtime-ai-planner-flow-expand";

        public const float AutoZoom = 0f;
        public const float MinDetailZoom = 0.25f;
        public const float MaxZoom = 1.8f;

        private const float MinOverviewZoom = 0.28f;
        private const float MaxDetailFitZoom = 1.25f;
        private const float ZoomStep = 0.15f;
        private const float OverviewPreviewHeight = 260f;
        private const float DetailPreviewHeight = 560f;
        private const float DefaultOverviewViewportWidth = 640f;
        private const float DefaultDetailViewportWidth = 1024f;
        private const float DefaultDetailViewportHeight = 560f;

        private readonly CharacterTestRuntimeAiPlannerEditorModel _model;
        private readonly CharacterTestRuntimeAiPlannerValidationReport _report;
        private readonly string _selectedStateId;
        private readonly int _selectedTransitionIndex;
        private readonly Action<string> _selectState;
        private readonly Action<int> _selectTransition;
        private readonly CharacterTestRuntimeAiPlannerFlowPreviewMode _mode;
        private readonly float _viewportWidth;
        private readonly float _viewportHeight;
        private readonly float _requestedZoom;
        private readonly Action<float> _setZoom;
        private readonly Action _fitZoom;
        private bool _isPanning;
        private int _panPointerId = -1;
        private Vector2 _lastPanPosition;

        public CharacterTestRuntimeAiPlannerFlowPreview(
            CharacterTestRuntimeAiPlannerEditorModel model,
            CharacterTestRuntimeAiPlannerValidationReport report,
            string selectedStateId,
            int selectedTransitionIndex,
            Action<string> selectState,
            Action<int> selectTransition,
            CharacterTestRuntimeAiPlannerFlowPreviewMode mode = CharacterTestRuntimeAiPlannerFlowPreviewMode.Overview,
            float viewportWidth = DefaultOverviewViewportWidth,
            float zoom = AutoZoom,
            Action<float> setZoom = null,
            Action fitZoom = null,
            float viewportHeight = 0f)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _report = report;
            _selectedStateId = selectedStateId ?? string.Empty;
            _selectedTransitionIndex = selectedTransitionIndex;
            _selectState = selectState;
            _selectTransition = selectTransition;
            _mode = mode;
            _viewportWidth = NormalizeViewportWidth(viewportWidth, mode);
            _viewportHeight = NormalizeViewportHeight(viewportHeight, mode);
            _requestedZoom = NormalizeRequestedZoom(zoom, mode);
            _setZoom = setZoom;
            _fitZoom = fitZoom;

            name = RootName;
            style.marginBottom = 10;
            style.minHeight = _viewportHeight + 42f;
            if (mode == CharacterTestRuntimeAiPlannerFlowPreviewMode.Detail)
            {
                style.flexGrow = 1f;
                style.minHeight = 0f;
            }

            Build();
        }

        public static string StateNodeName(string stateId)
        {
            return "runtime-ai-planner-flow-state-" + MakeNameKey(stateId);
        }

        public static string TransitionEdgeName(int transitionIndex)
        {
            return "runtime-ai-planner-flow-transition-" + transitionIndex;
        }

        private void Build()
        {
            float previewHeight = _viewportHeight;
            LayoutInput baseLayout = CreateLayout(FlowMetrics.Create(_mode, 1f), previewHeight);
            float effectiveZoom = ResolveEffectiveZoom(baseLayout.ContentBounds, previewHeight);
            FlowMetrics metrics = FlowMetrics.Create(_mode, effectiveZoom);
            LayoutInput layout = CreateLayout(metrics, previewHeight);

            Add(CreateHeader(Mathf.RoundToInt(effectiveZoom * 100f)));
            if (_model.States.Count == 0)
            {
                Add(new HelpBox("暂无状态。请先创建 root 或子状态。", HelpBoxMessageType.Info));
                return;
            }

            VisualElement viewport = _mode == CharacterTestRuntimeAiPlannerFlowPreviewMode.Overview
                ? CreateOverviewViewport(previewHeight)
                : CreateDetailViewport(previewHeight);
            viewport.Add(CreateCanvas(layout));
            Add(viewport);
        }

        private LayoutInput CreateLayout(FlowMetrics metrics, float previewHeight)
        {
            return BuildGraphLayout(metrics, previewHeight);
        }

        private VisualElement CreateCanvas(LayoutInput layout)
        {
            var canvas = new VisualElement { name = CanvasName };
            canvas.style.position = Position.Relative;
            canvas.style.width = layout.CanvasBounds.width;
            canvas.style.height = layout.CanvasBounds.height;
            canvas.style.minWidth = layout.CanvasBounds.width;
            canvas.style.minHeight = layout.CanvasBounds.height;
            canvas.style.backgroundColor = new Color(0.16f, 0.16f, 0.16f);

            canvas.Add(new EdgeLineLayer(layout.Edges)
            {
                name = "runtime-ai-planner-flow-lines",
                pickingMode = PickingMode.Ignore
            });

            AddGlobalNode(canvas, layout.GlobalRect, layout.Metrics);
            if (layout.HasMissingTarget)
                AddMissingTargetNode(canvas, layout.MissingTargetRect, layout.Metrics);

            for (int i = 0; i < _model.States.Count; i++)
            {
                CharacterTestRuntimeAiPlannerEditorModel.StateDraft state = _model.States[i];
                if (layout.NodeRects.TryGetValue(state.StateId, out Rect rect))
                    canvas.Add(CreateStateNode(state, rect, layout.Metrics));
            }

            AddTransitionRuleNodes(canvas, layout.Rules, layout.Metrics);
            return canvas;
        }

        private VisualElement CreateOverviewViewport(float previewHeight)
        {
            var viewport = new VisualElement { name = ViewportName };
            viewport.style.height = previewHeight;
            viewport.style.minHeight = previewHeight;
            viewport.style.width = Length.Percent(100f);
            viewport.style.minWidth = 0f;
            viewport.style.flexShrink = 0f;
            viewport.style.overflow = Overflow.Hidden;
            ApplyViewportBorder(viewport);
            return viewport;
        }

        private VisualElement CreateDetailViewport(float previewHeight)
        {
            var scroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal)
            {
                name = ScrollName
            };
            scroll.style.height = previewHeight;
            scroll.style.minHeight = previewHeight;
            scroll.style.width = Length.Percent(100f);
            scroll.style.minWidth = 0f;
            scroll.style.flexShrink = 0f;
            scroll.style.flexGrow = 1f;
            ApplyViewportBorder(scroll);
            RegisterDetailNavigation(scroll);
            return scroll;
        }

        private static void ApplyViewportBorder(VisualElement element)
        {
            element.style.borderLeftWidth = 1;
            element.style.borderRightWidth = 1;
            element.style.borderTopWidth = 1;
            element.style.borderBottomWidth = 1;
            element.style.borderLeftColor = new Color(0.24f, 0.24f, 0.24f);
            element.style.borderRightColor = new Color(0.24f, 0.24f, 0.24f);
            element.style.borderTopColor = new Color(0.24f, 0.24f, 0.24f);
            element.style.borderBottomColor = new Color(0.24f, 0.24f, 0.24f);
        }

        private VisualElement CreateHeader(int zoomPercent)
        {
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 5;

            var title = new Label("行为流程预览");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginRight = 8;
            header.Add(title);

            var hint = new Label(_mode == CharacterTestRuntimeAiPlannerFlowPreviewMode.Overview
                ? "默认显示完整概览；完整条件请看 tooltip、Inspector 或放大预览。"
                : "详细预览只读；点击状态或切换标签可定位 Inspector。");
            hint.style.opacity = 0.72f;
            hint.style.whiteSpace = WhiteSpace.Normal;
            hint.style.minWidth = 0;
            hint.style.flexGrow = 1;
            header.Add(hint);

            var controls = new VisualElement();
            controls.style.flexDirection = FlexDirection.Row;
            controls.style.alignItems = Align.Center;
            controls.style.flexShrink = 0;
            controls.Add(CreateHeaderButton("-", ZoomOutButtonName, () => RequestZoom(GetNextZoom(-ZoomStep)), _setZoom != null));

            var zoomLabel = new Label(_requestedZoom <= 0f ? "适应" : zoomPercent + "%");
            zoomLabel.style.minWidth = 44;
            zoomLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            zoomLabel.style.opacity = 0.78f;
            controls.Add(zoomLabel);

            controls.Add(CreateHeaderButton("+", ZoomInButtonName, () => RequestZoom(GetNextZoom(ZoomStep)), _setZoom != null));
            controls.Add(CreateHeaderButton("100%", ResetZoomButtonName, () => RequestZoom(1f), _setZoom != null));
            if (_mode == CharacterTestRuntimeAiPlannerFlowPreviewMode.Detail)
                controls.Add(CreateHeaderButton("居中", CenterButtonName, CenterDetailView, true));
            controls.Add(CreateHeaderButton("适应", FitZoomButtonName, () => _fitZoom?.Invoke(), _fitZoom != null));

            header.Add(controls);
            return header;
        }

        private static Button CreateHeaderButton(
            string text,
            string buttonName,
            Action onClick,
            bool enabled)
        {
            var button = new Button(onClick)
            {
                name = buttonName,
                text = text
            };
            button.style.marginLeft = 4;
            button.style.height = 22;
            button.style.minWidth = 34;
            button.style.paddingLeft = 6;
            button.style.paddingRight = 6;
            button.SetEnabled(enabled);
            return button;
        }

        private float GetNextZoom(float delta)
        {
            float current = _requestedZoom > 0f
                ? _requestedZoom
                : ResolveEffectiveZoom(
                    CreateLayout(FlowMetrics.Create(_mode, 1f), _viewportHeight).ContentBounds,
                    _viewportHeight);
            return current + delta;
        }

        private void RequestZoom(float zoom)
        {
            _setZoom?.Invoke(ClampZoom(zoom, _mode));
        }

        private void CenterDetailView()
        {
            ScrollView scroll = this.Q<ScrollView>(ScrollName);
            VisualElement canvas = this.Q(CanvasName);
            if (scroll == null || canvas == null)
                return;

            float canvasWidth = canvas.style.width.value.value;
            float canvasHeight = canvas.style.height.value.value;
            scroll.scrollOffset = new Vector2(
                Math.Max(0f, (canvasWidth - _viewportWidth) * 0.5f),
                Math.Max(0f, (canvasHeight - _viewportHeight) * 0.5f));
        }

        private void RegisterDetailNavigation(ScrollView scroll)
        {
            scroll.RegisterCallback<WheelEvent>(OnDetailWheel);
            scroll.RegisterCallback<PointerDownEvent>(OnDetailPointerDown);
            scroll.RegisterCallback<PointerMoveEvent>(OnDetailPointerMove);
            scroll.RegisterCallback<PointerUpEvent>(OnDetailPointerUp);
            scroll.RegisterCallback<PointerCancelEvent>(OnDetailPointerCancel);
        }

        private void OnDetailWheel(WheelEvent evt)
        {
            if (_setZoom == null || !evt.ctrlKey)
                return;

            RequestZoom(GetNextZoom(evt.delta.y < 0f ? ZoomStep : -ZoomStep));
            evt.StopPropagation();
        }

        private void OnDetailPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 2 && !(evt.button == 0 && evt.altKey))
                return;

            _isPanning = true;
            _panPointerId = evt.pointerId;
            _lastPanPosition = new Vector2(evt.position.x, evt.position.y);
            ((VisualElement)evt.currentTarget).CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnDetailPointerMove(PointerMoveEvent evt)
        {
            if (!_isPanning || evt.pointerId != _panPointerId)
                return;

            var scroll = evt.currentTarget as ScrollView;
            if (scroll == null)
                return;

            Vector2 current = new Vector2(evt.position.x, evt.position.y);
            Vector2 delta = current - _lastPanPosition;
            _lastPanPosition = current;
            scroll.scrollOffset -= delta;
            evt.StopPropagation();
        }

        private void OnDetailPointerUp(PointerUpEvent evt)
        {
            if (EndDetailPan(evt.pointerId, evt.currentTarget as VisualElement))
                evt.StopPropagation();
        }

        private void OnDetailPointerCancel(PointerCancelEvent evt)
        {
            if (EndDetailPan(evt.pointerId, evt.currentTarget as VisualElement))
                evt.StopPropagation();
        }

        private bool EndDetailPan(int pointerId, VisualElement currentTarget)
        {
            if (!_isPanning || pointerId != _panPointerId)
                return false;

            _isPanning = false;
            _panPointerId = -1;
            if (currentTarget != null && currentTarget.HasPointerCapture(pointerId))
                currentTarget.ReleasePointer(pointerId);
            return true;
        }

        private float ResolveEffectiveZoom(Rect baseBounds, float previewHeight)
        {
            if (_requestedZoom > 0f)
                return ClampZoom(_requestedZoom, _mode);

            float availableWidth = Math.Max(1f, _viewportWidth - 2f);
            float availableHeight = Math.Max(1f, previewHeight - 2f);
            float widthZoom = availableWidth / Math.Max(1f, baseBounds.xMax);
            float heightZoom = availableHeight / Math.Max(1f, baseBounds.yMax);
            float maxFitZoom = _mode == CharacterTestRuntimeAiPlannerFlowPreviewMode.Detail
                ? MaxDetailFitZoom
                : 1f;
            float fitZoom = Mathf.Min(maxFitZoom, widthZoom, heightZoom);
            return ClampZoom(fitZoom, _mode);
        }

    }
}

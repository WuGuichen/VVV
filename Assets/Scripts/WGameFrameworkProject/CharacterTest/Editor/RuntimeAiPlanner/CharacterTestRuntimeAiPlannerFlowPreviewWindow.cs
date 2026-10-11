using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal sealed class CharacterTestRuntimeAiPlannerFlowPreviewWindow : EditorWindow
    {
        private CharacterTestRuntimeAiPlannerEditorModel _model;
        private string _selectedStateId = string.Empty;
        private int _selectedTransitionIndex = -1;
        private Action<string> _selectState;
        private Action<int> _selectTransition;
        private float _zoom = CharacterTestRuntimeAiPlannerFlowPreview.AutoZoom;
        private Vector2 _lastRootSize;

        public static CharacterTestRuntimeAiPlannerFlowPreviewWindow Open(
            CharacterTestRuntimeAiPlannerEditorModel model,
            string selectedStateId,
            int selectedTransitionIndex,
            Action<string> selectState,
            Action<int> selectTransition)
        {
            CharacterTestRuntimeAiPlannerFlowPreviewWindow window =
                GetWindow<CharacterTestRuntimeAiPlannerFlowPreviewWindow>();
            window.titleContent = new GUIContent("Runtime AI Planner Flow");
            window.minSize = new Vector2(760f, 520f);
            window._zoom = CharacterTestRuntimeAiPlannerFlowPreview.AutoZoom;
            window.SetContext(model, selectedStateId, selectedTransitionIndex, selectState, selectTransition);
            window.Show();
            return window;
        }

        public void CreateGUI()
        {
            rootVisualElement.UnregisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
            rootVisualElement.RegisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
            RefreshView();
        }

        private void SetContext(
            CharacterTestRuntimeAiPlannerEditorModel model,
            string selectedStateId,
            int selectedTransitionIndex,
            Action<string> selectState,
            Action<int> selectTransition)
        {
            _model = model;
            _selectedStateId = selectedStateId ?? string.Empty;
            _selectedTransitionIndex = selectedTransitionIndex;
            _selectState = selectState;
            _selectTransition = selectTransition;
            RefreshView();
        }

        private void RefreshView()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 8;
            rootVisualElement.style.paddingBottom = 8;
            if (_model == null)
            {
                rootVisualElement.Add(new HelpBox("未加载 Runtime AI Planner profile。", HelpBoxMessageType.Info));
                return;
            }

            Vector2 viewportSize = CalculateViewportSize();
            rootVisualElement.Add(new CharacterTestRuntimeAiPlannerFlowPreview(
                _model,
                _model.Validate(),
                _selectedStateId,
                _selectedTransitionIndex,
                SelectStateFromPreview,
                SelectTransitionFromPreview,
                CharacterTestRuntimeAiPlannerFlowPreviewMode.Detail,
                viewportSize.x,
                _zoom,
                SetZoom,
                FitZoom,
                viewportSize.y));
        }

        private void SelectStateFromPreview(string stateId)
        {
            _selectedStateId = stateId ?? string.Empty;
            _selectedTransitionIndex = -1;
            _selectState?.Invoke(stateId);
            RefreshView();
        }

        private void SelectTransitionFromPreview(int transitionIndex)
        {
            _selectedTransitionIndex = transitionIndex;
            _selectTransition?.Invoke(transitionIndex);
            RefreshView();
        }

        private void SetZoom(float zoom)
        {
            _zoom = Mathf.Clamp(
                zoom,
                CharacterTestRuntimeAiPlannerFlowPreview.MinDetailZoom,
                CharacterTestRuntimeAiPlannerFlowPreview.MaxZoom);
            RefreshView();
        }

        private void FitZoom()
        {
            _zoom = CharacterTestRuntimeAiPlannerFlowPreview.AutoZoom;
            RefreshView();
        }

        private void OnRootGeometryChanged(GeometryChangedEvent evt)
        {
            if (Mathf.Abs(evt.newRect.width - _lastRootSize.x) < 1f
                && Mathf.Abs(evt.newRect.height - _lastRootSize.y) < 1f)
            {
                return;
            }

            _lastRootSize = evt.newRect.size;
            RefreshView();
        }

        private Vector2 CalculateViewportSize()
        {
            float width = rootVisualElement.resolvedStyle.width;
            float height = rootVisualElement.resolvedStyle.height;
            if (width <= 1f)
                width = position.width;
            if (height <= 1f)
                height = position.height;

            return new Vector2(
                Mathf.Max(320f, width - 16f),
                Mathf.Max(280f, height - 58f));
        }
    }
}

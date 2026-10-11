using System;
using MxFramework.Combat.Authoring;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Combat.Editor
{
    [Overlay(typeof(SceneView), "Combat Authoring", true)]
    public sealed class CombatSceneOverlay : Overlay
    {
        private Label _assetLabel;
        private Label _bindingLabel;
        private Label _frameLabel;
        private SliderInt _frameSlider;
        private ToolbarToggle _actorToggle;
        private ToolbarToggle _bodyToggle;
        private ToolbarToggle _colliderToggle;
        private ToolbarToggle _traceToggle;
        private ToolbarToggle _labelsToggle;

        public override VisualElement CreatePanelContent()
        {
            var root = new VisualElement();
            root.style.minWidth = 260;
            root.style.maxWidth = 340;
            root.style.paddingLeft = 6;
            root.style.paddingRight = 6;
            root.style.paddingTop = 6;
            root.style.paddingBottom = 6;

            _assetLabel = CreateSummaryLabel();
            _bindingLabel = CreateSummaryLabel();
            _frameLabel = CreateSummaryLabel();
            root.Add(_assetLabel);
            root.Add(_bindingLabel);

            var frameRow = new Toolbar();
            frameRow.style.flexDirection = FlexDirection.Row;
            frameRow.style.marginTop = 4;
            frameRow.Add(new ToolbarButton(() => StepFrame(-1)) { text = "<" });
            _frameSlider = new SliderInt(string.Empty, 0, 0);
            _frameSlider.style.flexGrow = 1;
            _frameSlider.RegisterValueChangedCallback(evt => CombatAuthoringSceneState.SetFrame(evt.newValue));
            frameRow.Add(_frameSlider);
            frameRow.Add(new ToolbarButton(() => StepFrame(1)) { text = ">" });
            root.Add(frameRow);
            root.Add(_frameLabel);

            var visibilityRow = new Toolbar();
            visibilityRow.style.flexWrap = Wrap.Wrap;
            visibilityRow.style.marginTop = 4;
            _actorToggle = CreateVisibilityToggle("Actor", value =>
            {
                var visibility = CombatAuthoringSceneState.Visibility;
                visibility.Actor = value;
                CombatAuthoringSceneState.SetVisibility(visibility);
            });
            _bodyToggle = CreateVisibilityToggle("Body", value =>
            {
                var visibility = CombatAuthoringSceneState.Visibility;
                visibility.Body = value;
                CombatAuthoringSceneState.SetVisibility(visibility);
            });
            _colliderToggle = CreateVisibilityToggle("Collider", value =>
            {
                var visibility = CombatAuthoringSceneState.Visibility;
                visibility.Collider = value;
                CombatAuthoringSceneState.SetVisibility(visibility);
            });
            _traceToggle = CreateVisibilityToggle("Trace", value =>
            {
                var visibility = CombatAuthoringSceneState.Visibility;
                visibility.Trace = value;
                CombatAuthoringSceneState.SetVisibility(visibility);
            });
            _labelsToggle = CreateVisibilityToggle("Labels", value =>
            {
                var visibility = CombatAuthoringSceneState.Visibility;
                visibility.Labels = value;
                CombatAuthoringSceneState.SetVisibility(visibility);
            });
            visibilityRow.Add(_actorToggle);
            visibilityRow.Add(_bodyToggle);
            visibilityRow.Add(_colliderToggle);
            visibilityRow.Add(_traceToggle);
            visibilityRow.Add(_labelsToggle);
            root.Add(visibilityRow);

            CombatAuthoringSceneState.Changed += Refresh;
            root.RegisterCallback<DetachFromPanelEvent>(_ => CombatAuthoringSceneState.Changed -= Refresh);
            Refresh();
            return root;
        }

        private static Label CreateSummaryLabel()
        {
            var label = new Label();
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            return label;
        }

        private static ToolbarToggle CreateVisibilityToggle(string text, Action<bool> setValue)
        {
            var toggle = new ToolbarToggle { text = text };
            toggle.RegisterValueChangedCallback(evt => setValue(evt.newValue));
            return toggle;
        }

        private void StepFrame(int delta)
        {
            CombatAuthoringSceneState.SetFrame(CombatAuthoringSceneState.Frame + delta);
        }

        private void Refresh()
        {
            CombatActionAuthoringAsset actionAsset = CombatAuthoringSceneState.ActionAsset;
            CombatSceneBindingAsset bindingAsset = CombatAuthoringSceneState.SceneBindingAsset;
            int frame = CombatAuthoringSceneState.Frame;
            int maxFrame = Math.Max(0, actionAsset == null ? 0 : actionAsset.TotalFrames - 1);

            if (_assetLabel != null)
            {
                _assetLabel.text = actionAsset == null
                    ? "Asset: 未选择"
                    : "Asset: " + actionAsset.name + " / Action " + actionAsset.ActionId;
            }

            if (_bindingLabel != null)
            {
                _bindingLabel.text = bindingAsset == null
                    ? "Binding: 未选择"
                    : "Binding: " + bindingAsset.name;
            }

            if (_frameLabel != null)
            {
                _frameLabel.text = "Frame: " + frame + " / " + maxFrame;
            }

            if (_frameSlider != null)
            {
                _frameSlider.lowValue = 0;
                _frameSlider.highValue = maxFrame;
                if (_frameSlider.value != frame)
                {
                    _frameSlider.SetValueWithoutNotify(frame);
                }
            }

            CombatAuthoringVisibility visibility = CombatAuthoringSceneState.Visibility;
            SetToggleWithoutNotify(_actorToggle, visibility.Actor);
            SetToggleWithoutNotify(_bodyToggle, visibility.Body);
            SetToggleWithoutNotify(_colliderToggle, visibility.Collider);
            SetToggleWithoutNotify(_traceToggle, visibility.Trace);
            SetToggleWithoutNotify(_labelsToggle, visibility.Labels);
        }

        private static void SetToggleWithoutNotify(ToolbarToggle toggle, bool value)
        {
            if (toggle != null && toggle.value != value)
            {
                toggle.SetValueWithoutNotify(value);
            }
        }
    }
}

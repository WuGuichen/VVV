using System;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>
    /// Lightweight FairyGUI Toast presenter rendered on MxUiLayer.Toast.
    /// Provides smooth fade-in, float-up, and fade-out visual feedback for dev panel operations and errors.
    /// </summary>
    /// <remarks>AllocByDesign during initial construction; NoAlloc during runtime Tick and Show calls.</remarks>
    internal sealed class CharacterTestFairyGuiToastPresenter : IDisposable
    {
        public const float DefaultEnterDuration = 0.16f;
        public const float DefaultExitDuration = 0.28f;
        public const float DefaultDisplayDuration = 3.0f;
        private const float DefaultWidth = 440f;
        private const float DefaultHeight = 48f;
        private const float TargetY = 100f;
        private const float DriftY = 14f;

        private static readonly Color WarningBorderColor = new Color(0.92f, 0.38f, 0.41f, 0.95f);
        private static readonly Color WarningFillColor = new Color(0.12f, 0.15f, 0.20f, 0.95f);
        private static readonly Color WarningTextColor = new Color(1f, 0.90f, 0.90f, 1f);

        private static readonly Color InfoBorderColor = new Color(0.35f, 0.72f, 0.65f, 0.95f);
        private static readonly Color InfoFillColor = new Color(0.10f, 0.16f, 0.18f, 0.95f);
        private static readonly Color InfoTextColor = new Color(0.88f, 0.98f, 0.94f, 1f);

        private MxFairyGuiLayerHost _layerHost;
        private Fgui.GComponent _toastRoot;
        private Fgui.GComponent _container;
        private Fgui.GGraph _background;
        private Fgui.GTextField _textField;

        private float _displayDuration;
        private float _elapsed;
        private bool _active;
        private bool _disposed;
        private float _currentWidth = DefaultWidth;

        public bool IsActive => _active;

        public void Configure(MxFairyGuiLayerHost layerHost)
        {
            _disposed = false;
            _layerHost = layerHost;
            EnsureView();
        }

        public void Show(string message, bool isWarning = true, float duration = DefaultDisplayDuration)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            _disposed = false;
            EnsureView();
            if (_container == null || _toastRoot == null)
                return;

            _displayDuration = duration > 0f ? duration : DefaultDisplayDuration;
            _elapsed = 0f;
            _active = true;

            // Update text and colors
            Color borderColor = isWarning ? WarningBorderColor : InfoBorderColor;
            Color fillColor = isWarning ? WarningFillColor : InfoFillColor;
            Color textColor = isWarning ? WarningTextColor : InfoTextColor;

            string formattedText = isWarning ? "⚠️ " + message.Trim() : "ℹ️ " + message.Trim();
            _textField.text = formattedText;
            _textField.textFormat.color = textColor;

            // Adaptive width based on message length
            float estimatedWidth = Mathf.Clamp(formattedText.Length * 16f + 64f, DefaultWidth, 720f);
            _currentWidth = estimatedWidth;
            _background.DrawRect(estimatedWidth, DefaultHeight, 2, borderColor, fillColor);
            _textField.SetSize(estimatedWidth, DefaultHeight);
            _container.SetSize(estimatedWidth, DefaultHeight);

            // Center horizontally on stage
            float rootWidth = _toastRoot.width > 0f ? _toastRoot.width : (Fgui.GRoot.inst?.width ?? 1920f);
            _container.SetPosition((rootWidth - estimatedWidth) / 2f, TargetY + DriftY, 0f);
            _container.alpha = 0f;
            _container.visible = true;

            // Ensure container is on top inside the Toast layer
            if (_container.parent != _toastRoot)
            {
                _toastRoot.AddChild(_container);
            }
            else
            {
                _toastRoot.SetChildIndex(_container, _toastRoot.numChildren - 1);
            }
        }

        public void Tick(float deltaSeconds)
        {
            if (!_active || _disposed || _container == null)
                return;

            float safeDelta = deltaSeconds > 0f ? deltaSeconds : 1f / 60f;
            _elapsed += safeDelta;

            float totalDuration = DefaultEnterDuration + _displayDuration + DefaultExitDuration;
            if (_elapsed >= totalDuration)
            {
                Hide();
                return;
            }

            float rootWidth = _toastRoot?.width > 0f ? _toastRoot.width : (Fgui.GRoot.inst?.width ?? 1920f);
            float targetX = (rootWidth - _currentWidth) / 2f;

            if (_elapsed < DefaultEnterDuration)
            {
                // Enter animation: fade in and slide up slightly
                float progress = Mathf.Clamp01(_elapsed / DefaultEnterDuration);
                _container.alpha = progress;
                _container.y = TargetY + DriftY * (1f - progress);
            }
            else if (_elapsed < DefaultEnterDuration + _displayDuration)
            {
                // Hold animation
                _container.alpha = 1f;
                _container.y = TargetY;
            }
            else
            {
                // Exit animation: fade out and slide up
                float exitElapsed = _elapsed - (DefaultEnterDuration + _displayDuration);
                float progress = Mathf.Clamp01(exitElapsed / DefaultExitDuration);
                _container.alpha = 1f - progress;
                _container.y = TargetY - DriftY * progress;
            }

            _container.x = targetX;
        }

        public void Hide()
        {
            _active = false;
            _elapsed = 0f;
            // Scene/test teardown may dispose FairyGUI's DisplayObject before the owning panel
            // releases its presenter. Avoid forwarding visibility to an already-detached object.
            if (!_disposed
                && _container != null
                && _container.displayObject != null
                && _container.displayObject.gameObject != null)
            {
                _container.visible = false;
                _container.alpha = 0f;
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _active = false;
            if (_container != null)
            {
                _container.Dispose();
                _container = null;
                _background = null;
                _textField = null;
            }
            _toastRoot = null;
            _layerHost = null;
        }

        private void EnsureView()
        {
            if (_container != null && !_container.isDisposed && _toastRoot != null && !_toastRoot.isDisposed)
                return;

            MxFairyGuiLayerHost host = _layerHost ?? CharacterTestFairyGuiLayerRoots.SharedLayerHost;
            CharacterTestFairyGuiLayerRoots.EnsureLayerRoots(host);

            if (!host.TryGetLayerRoot(MxUiLayer.Toast, out Fgui.GComponent layerRoot) || layerRoot == null)
            {
                layerRoot = Fgui.GRoot.inst;
            }
            _toastRoot = layerRoot;
            if (_toastRoot == null)
                return;

            _container = new Fgui.GComponent
            {
                name = "CharacterTest.DevToastPresenter",
                touchable = false,
                visible = false,
                alpha = 0f
            };
            _container.SetSize(DefaultWidth, DefaultHeight);

            _background = new Fgui.GGraph();
            _background.DrawRect(DefaultWidth, DefaultHeight, 2, WarningBorderColor, WarningFillColor);
            _container.AddChild(_background);

            _textField = new Fgui.GTextField
            {
                touchable = false,
                align = Fgui.AlignType.Center,
                verticalAlign = Fgui.VertAlignType.Middle,
                autoSize = Fgui.AutoSizeType.None,
                singleLine = true
            };
            _textField.textFormat.size = 18;
            _textField.textFormat.bold = true;
            _textField.SetSize(DefaultWidth, DefaultHeight);
            _container.AddChild(_textField);

            _toastRoot.AddChild(_container);
        }
    }
}

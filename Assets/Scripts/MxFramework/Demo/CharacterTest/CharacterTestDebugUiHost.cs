using System;
using MxFramework.DebugUI;
using MxFramework.DebugUI.Toolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Demo.CharacterTest
{
    /// <summary>
    /// Mounts the generic Debug UI overlay for CharacterTest and binds it to <see cref="GameSlice"/> debug sources.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    [AddComponentMenu("MxFramework/Demo/Character Test/Character Test Debug UI Host")]
    public sealed class CharacterTestDebugUiHost : MonoBehaviour
    {
        [SerializeField] private PanelSettings _panelSettings;
        [SerializeField] private DebugUiVisibility _initialVisibility = DebugUiVisibility.Collapsed;
        [SerializeField] private bool _showOnStart = true;
        [SerializeField] private int _initialTabIndex;

        private DebugUiOverlayController _overlay;
        private GameSlice _slice;
        private Func<bool> _isPausedProvider;

        public DebugUiOverlayController Overlay => _overlay;

        public void Configure(
            GameSlice slice,
            Func<bool> isPausedProvider,
            PanelSettings panelSettings = null,
            DebugUiVisibility? initialVisibility = null,
            bool? showOnStart = null)
        {
            _slice = slice;
            _isPausedProvider = isPausedProvider;
            if (panelSettings != null)
                _panelSettings = panelSettings;
            if (initialVisibility.HasValue)
                _initialVisibility = initialVisibility.Value;
            bool reveal = showOnStart ?? _showOnStart;

            EnsureOverlay();
            if (_slice == null)
                return;

            FrameworkDebugSourceRegistry registry = _slice.CreateDebugSourceRegistry(_isPausedProvider);
            _overlay.Configure(registry);

            if (reveal)
            {
                _overlay.SetVisibility(_initialVisibility);
                _overlay.SetActiveTab(_initialTabIndex);
                _overlay.RefreshNow();
            }
            else
            {
                _overlay.SetVisibility(DebugUiVisibility.Hidden);
            }
        }

        public void Release()
        {
            _slice = null;
            _isPausedProvider = null;
            if (_overlay != null)
                _overlay.SetVisibility(DebugUiVisibility.Hidden);
        }

        private void EnsureOverlay()
        {
            if (_overlay == null)
                _overlay = GetComponent<DebugUiOverlayController>() ?? gameObject.AddComponent<DebugUiOverlayController>();

            if (_panelSettings != null)
                _overlay.ConfigurePanelSettings(_panelSettings);
        }
    }
}

using System;
using MxFramework.DebugUI;
using MxFramework.Runtime;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>
    /// Mounts the CharacterTest developer-only debug panel.
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

        private CharacterTestDeveloperDebugPanel _panel;
        private CharacterTestSliceServices _services;
        private Func<bool> _isPausedProvider;

        public CharacterTestDeveloperDebugPanel Panel => _panel;

        public void Configure(
            GameSlice slice,
            Func<bool> isPausedProvider,
            PanelSettings panelSettings = null,
            DebugUiVisibility? initialVisibility = null,
            bool? showOnStart = null,
            int? initialTabIndex = null,
            IRuntimeLogger logger = null)
        {
            Configure(
                slice?.Services,
                isPausedProvider,
                panelSettings,
                initialVisibility,
                showOnStart,
                initialTabIndex,
                logger);
        }

        internal void Configure(
            CharacterTestSliceServices services,
            Func<bool> isPausedProvider,
            PanelSettings panelSettings = null,
            DebugUiVisibility? initialVisibility = null,
            bool? showOnStart = null,
            int? initialTabIndex = null,
            IRuntimeLogger logger = null)
        {
            _services = services;
            _isPausedProvider = isPausedProvider;
            if (panelSettings != null)
                _panelSettings = panelSettings;
            if (initialVisibility.HasValue)
                _initialVisibility = initialVisibility.Value;
            if (initialTabIndex.HasValue)
                _initialTabIndex = initialTabIndex.Value;
            bool reveal = showOnStart ?? _showOnStart;

            EnsurePanel();
            if (_services == null)
                return;

            _panel.Configure(
                _services,
                _isPausedProvider,
                _panelSettings,
                _initialVisibility,
                reveal,
                _initialTabIndex,
                logger);
        }

        public void Release()
        {
            _services = null;
            _isPausedProvider = null;
            if (_panel != null)
                _panel.Release();
        }

        private void EnsurePanel()
        {
            if (_panel == null)
                _panel = GetComponent<CharacterTestDeveloperDebugPanel>() ?? gameObject.AddComponent<CharacterTestDeveloperDebugPanel>();
        }
    }
}

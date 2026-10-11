using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.DebugUI;
using MxFramework.DebugUI.Toolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.CharacterRuntimeSpawn.DebugUI.Unity
{
    [DefaultExecutionOrder(900)]
    [DisallowMultipleComponent]
    [AddComponentMenu("MxFramework/Character/Runtime Animation Debug Panel")]
    public sealed class CharacterRuntimeAnimationDebugPanel : MonoBehaviour
    {
        [SerializeField] private CharacterRuntimeResourceBootstrap _bootstrap;
        [SerializeField] private DebugUiOverlayController _overlay;
        [SerializeField] private PanelSettings _panelSettings;
        [SerializeField] private DebugUiVisibility _initialVisibility = DebugUiVisibility.Expanded;
        [SerializeField] private int _initialTabIndex = 1;
        [SerializeField] private bool _showOnStart = true;

        private CharacterRuntimeAnimationDebugSource _source;
        private bool _registered;

        public CharacterRuntimeAnimationDebugSource Source => _source;
        public DebugUiOverlayController Overlay => _overlay;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            RegisterSource();
        }

        private void Start()
        {
            ResolveReferences();
            RegisterSource();

            if (_showOnStart && _overlay != null)
            {
                _overlay.SetVisibility(_initialVisibility);
                _overlay.SetActiveTab(_initialTabIndex);
                _overlay.RefreshNow();
            }
        }

        private void OnDisable()
        {
            if (_registered && _overlay != null && _source != null)
                _overlay.UnregisterSource(_source.Name);
            _registered = false;
        }

        public void Configure(
            CharacterRuntimeResourceBootstrap bootstrap,
            DebugUiOverlayController overlay = null,
            PanelSettings panelSettings = null,
            DebugUiVisibility initialVisibility = DebugUiVisibility.Expanded,
            int initialTabIndex = 1)
        {
            _bootstrap = bootstrap;
            _overlay = overlay;
            _panelSettings = panelSettings;
            _initialVisibility = initialVisibility;
            _initialTabIndex = initialTabIndex;
            _source = null;
            _registered = false;
            RegisterSource();
        }

        private void RegisterSource()
        {
            if (_registered || _overlay == null)
                return;

            if (_source == null)
                _source = new CharacterRuntimeAnimationDebugSource(_bootstrap);

            _registered = _overlay.RegisterSource(_source);
        }

        private void ResolveReferences()
        {
            if (_bootstrap == null)
                _bootstrap = GetComponent<CharacterRuntimeResourceBootstrap>();
            if (_overlay == null)
                _overlay = GetComponent<DebugUiOverlayController>() ?? gameObject.AddComponent<DebugUiOverlayController>();
            if (_panelSettings != null)
                _overlay.ConfigurePanelSettings(_panelSettings);
        }
    }
}

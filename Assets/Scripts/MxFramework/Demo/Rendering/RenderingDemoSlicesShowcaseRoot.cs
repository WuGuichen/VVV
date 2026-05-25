using System.Collections.Generic;
using MxFramework.Rendering;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Demo.Rendering
{
    [DisallowMultipleComponent]
    [AddComponentMenu("MxFramework/Demo/Rendering Demo Slices Showcase Root")]
    public sealed class RenderingDemoSlicesShowcaseRoot : MonoBehaviour
    {
        [SerializeField] private UIDocument _document = null;
        [SerializeField] private VisualTreeAsset _visualTree = null;
        [SerializeField] private StyleSheet _styleSheet = null;
        [SerializeField] private RenderingDemoSlicesHudController _hud = null;
        [SerializeField] private Renderer _subjectRenderer = null;
        [SerializeField] private Transform _windArrow = null;
        [SerializeField] private Transform _subjectMarker = null;

        private RenderingDemoSlicesShowcaseRuntime _runtime;
        private bool _initialized;

        public bool IsInitialized => _initialized;
        public RenderingDemoSlicesSnapshot Snapshot => _runtime != null ? _runtime.Snapshot : default;
        private VisualTreeAsset VisualTree => _visualTree;
        private StyleSheet StyleSheet => _styleSheet;

        public void ConfigureSceneReferences(
            UIDocument document,
            VisualTreeAsset visualTree,
            StyleSheet styleSheet,
            RenderingDemoSlicesHudController hud,
            Renderer subjectRenderer,
            Transform windArrow,
            Transform subjectMarker)
        {
            _document = document;
            _visualTree = visualTree;
            _styleSheet = styleSheet;
            _hud = hud;
            _subjectRenderer = subjectRenderer;
            _windArrow = windArrow;
            _subjectMarker = subjectMarker;
        }

        private void Awake()
        {
            Initialize();
        }

        private void Update()
        {
            if (!_initialized || _runtime == null)
                return;

            EnqueueKeyboardCommands();
            _runtime.Step(Time.deltaTime);
            ApplyScenePresentation(_runtime.Snapshot);
            _hud?.Refresh(_runtime.Snapshot);
        }

        private void OnDestroy()
        {
            _runtime?.Dispose();
            _runtime = null;
            _initialized = false;
        }

        public void EnqueueCommand(RenderingDemoCommand command)
        {
            _runtime?.Enqueue(command);
        }

        public void InitializeForValidation()
        {
            Initialize();
        }

        public void RunFrameForValidation(float deltaTime)
        {
            Initialize();
            if (_runtime == null)
                return;

            _runtime.Step(deltaTime);
            ApplyScenePresentation(_runtime.Snapshot);
            _hud?.Refresh(_runtime.Snapshot);
        }

        private void Initialize()
        {
            if (_initialized)
                return;

            _document = _document != null ? _document : GetComponent<UIDocument>();
            _hud = _hud != null ? _hud : GetComponent<RenderingDemoSlicesHudController>();
            _hud?.Configure(this, _document, VisualTree, StyleSheet);

            _runtime = new RenderingDemoSlicesShowcaseRuntime();
            _runtime.Initialize(_subjectRenderer, FindFrameworkPipelines());
            _initialized = true;
            _hud?.Refresh(_runtime.Snapshot);
        }

        private static IEnumerable<IMxRenderPipeline> FindFrameworkPipelines()
        {
            MxRenderingPipelineFeature[] features = UnityEngine.Resources.FindObjectsOfTypeAll<MxRenderingPipelineFeature>();
            for (int i = 0; i < features.Length; i++)
            {
                if (features[i] != null)
                    yield return features[i].Pipeline;
            }
        }

        private void EnqueueKeyboardCommands()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha1))
                EnqueueCommand(RenderingDemoCommand.ToggleWind);
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha2))
                EnqueueCommand(RenderingDemoCommand.PulseMaterial);
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha3))
                EnqueueCommand(RenderingDemoCommand.PublishEventBurst);
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha4))
                EnqueueCommand(RenderingDemoCommand.CycleVolumePriority);
            if (UnityEngine.Input.GetKeyDown(KeyCode.R))
                EnqueueCommand(RenderingDemoCommand.Reset);
        }

        private void ApplyScenePresentation(RenderingDemoSlicesSnapshot snapshot)
        {
            if (_windArrow != null)
            {
                Vector3 wind = snapshot.Globals.WindDirection;
                if (wind.sqrMagnitude > 0.001f)
                    _windArrow.rotation = Quaternion.LookRotation(wind.normalized, Vector3.up);
            }

            if (_subjectMarker != null)
                _subjectMarker.position = snapshot.Globals.PrimarySubjectWorldPos;
        }
    }
}

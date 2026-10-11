using MxFramework.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MxFramework.Samples.RuntimeHostMinimalLifecycle
{
    /// <summary>Visible, self-contained RuntimeHost lifecycle sample.</summary>
    public sealed class RuntimeHostMinimalLifecycleSample : MonoBehaviour
    {
        private const string SceneName = "RuntimeHostMinimalLifecycle";
        private const string ExpectedEvents = "initialize,start,tick:0,tick:1,tick:2,stop,dispose";

        private RuntimeHost _host;
        private ProbeModule _module;
        private long _frame;
        private double _elapsed;
        private string _status = "Waiting";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallForSampleScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !string.Equals(scene.name, SceneName, System.StringComparison.Ordinal))
                return;
            if (FindAnyObjectByType<RuntimeHostMinimalLifecycleSample>() != null)
                return;

            var root = new GameObject("[MxFramework Sample] RuntimeHost Minimal Lifecycle");
            root.AddComponent<RuntimeHostMinimalLifecycleSample>();
        }

        private void Start()
        {
            try
            {
                _module = new ProbeModule();
                _host = new RuntimeHost();
                _host.RegisterModule(_module);
                _host.Initialize();
                _host.Start();
                _status = "Running";
            }
            catch (System.Exception exception)
            {
                _status = "Failed: " + exception.Message;
                Cleanup();
            }
        }

        private void Update()
        {
            if (_host == null || _status != "Running")
                return;

            float deltaTime = Time.deltaTime;
            _elapsed += deltaTime;
            _host.Tick(_frame++, deltaTime, _elapsed);
            if (_frame < 3)
                return;

            _host.Stop();
            _host.Dispose();
            bool passed = _host.TickCount == 3
                && _host.Errors.Count == 0
                && string.Equals(_module.Events, ExpectedEvents, System.StringComparison.Ordinal);
            _status = passed ? "Passed" : "Failed: " + _module.Events;
            _host = null;
        }

        private void OnDestroy()
        {
            Cleanup();
        }

        private void OnGUI()
        {
            GUI.Box(new Rect(24f, 24f, 620f, 128f), "MxFramework RuntimeHost Minimal Lifecycle");
            GUI.Label(new Rect(44f, 58f, 580f, 24f), "RuntimeHost lifecycle: " + _status);
            GUI.Label(new Rect(44f, 84f, 580f, 44f), "Expected: " + ExpectedEvents);
            GUI.Label(new Rect(44f, 112f, 580f, 24f), "Observed: " + (_module != null ? _module.Events : string.Empty));
        }

        private void Cleanup()
        {
            if (_host == null)
                return;

            _host.Stop();
            _host.Dispose();
            _host = null;
        }

        private sealed class ProbeModule : RuntimeModule
        {
            public ProbeModule()
                : base("sample.runtime-host.probe")
            {
            }

            public string Events { get; private set; } = string.Empty;

            public override void Initialize(RuntimeHostContext context) => Append("initialize");
            public override void Start(RuntimeHostContext context) => Append("start");
            /// <remarks>LowFreqAlloc; this sample records only three diagnostic ticks.</remarks>
            public override void Tick(RuntimeTickContext context) => Append("tick:" + context.FrameIndex);
            public override void Stop(RuntimeHostContext context) => Append("stop");
            public override void Dispose() => Append("dispose");

            private void Append(string value)
            {
                Events = Events.Length == 0 ? value : Events + "," + value;
            }
        }
    }
}

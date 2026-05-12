using UnityEngine;
using UnityEngine.SceneManagement;

namespace MxFramework.Demo
{
    [AddComponentMenu("MxFramework/Demo/Runtime Vertical Slice Bootstrap")]
    public sealed class RuntimeVerticalSliceBootstrap : MonoBehaviour
    {
        [SerializeField] private RuntimeVerticalSliceSceneConfig _config;
        private bool _started;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartFromConfig()
        {
            RuntimeVerticalSliceBootstrap bootstrap = Object.FindFirstObjectByType<RuntimeVerticalSliceBootstrap>();
            if (bootstrap != null)
            {
                bootstrap.TryStart();
                return;
            }

            RuntimeVerticalSliceSceneConfig config = RuntimeVerticalSliceSceneConfig.LoadDefault();
            TryCreateRunner(config);
        }

        private void Start()
        {
            TryStart();
        }

        private void TryStart()
        {
            if (_started)
                return;

            _started = true;
            RuntimeVerticalSliceSceneConfig config = _config != null ? _config : RuntimeVerticalSliceSceneConfig.LoadDefault();
            TryCreateRunner(config);
        }

        private static void TryCreateRunner(RuntimeVerticalSliceSceneConfig config)
        {
            if (config == null || !config.AutoStartInScene)
                return;

            Scene scene = SceneManager.GetActiveScene();
            if (!string.Equals(scene.name, config.SceneName, System.StringComparison.Ordinal))
                return;

            if (Object.FindFirstObjectByType<RuntimeVerticalSliceRunner>() != null)
                return;

            var go = new GameObject("RuntimeVerticalSliceRuntime");
            var runner = go.AddComponent<RuntimeVerticalSliceRunner>();
            runner.ApplyConfig(config);
        }
    }
}

using MxFramework.Runtime;
using UnityEngine.SceneManagement;

namespace MxFramework.Gameplay.Triggers.Unity
{
    /// <summary>Persistent Runtime boundary that flushes Unity occupancy before Gameplay drains commands.</summary>
    public sealed class GameplayTriggerUnityOccupancyFlushModule : RuntimeModule
    {
        public const string DefaultModuleId = "mxframework.gameplay.triggers.unity_occupancy";
        public const int DefaultPriority = 0;

        private readonly GameplayTriggerUnityOccupancyAdapter _adapter;
        private bool _sceneCallbackRegistered;

        internal GameplayTriggerUnityOccupancyFlushModule(
            GameplayTriggerUnityOccupancyAdapter adapter)
            : base(DefaultModuleId, RuntimeTickStage.PreSimulation, DefaultPriority)
        {
            _adapter = adapter;
        }

        /// <remarks>LowFreqAlloc. Registers the Unity scene lifecycle callback.</remarks>
        public override void Start(RuntimeHostContext context)
        {
            if (_sceneCallbackRegistered)
                return;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            _sceneCallbackRegistered = true;
        }

        /// <remarks>NoAlloc after initialization.</remarks>
        public override void Tick(RuntimeTickContext context)
        {
            _adapter.Flush(new RuntimeFrame(context.FrameIndex));
        }

        /// <remarks>LowFreqAlloc. Releases the Unity scene lifecycle callback.</remarks>
        public override void Stop(RuntimeHostContext context)
        {
            UnregisterSceneCallback();
        }

        /// <remarks>LowFreqAlloc. Idempotently releases the Unity scene lifecycle callback.</remarks>
        public override void Dispose()
        {
            UnregisterSceneCallback();
        }

        private void OnSceneUnloaded(Scene scene)
        {
            _adapter.NotifySceneUnloaded(scene.handle.GetRawData());
        }

        private void UnregisterSceneCallback()
        {
            if (!_sceneCallbackRegistered)
                return;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            _sceneCallbackRegistered = false;
        }
    }
}

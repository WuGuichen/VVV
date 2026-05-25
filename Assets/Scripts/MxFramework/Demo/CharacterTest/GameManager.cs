using UnityEngine;

namespace MxFramework.Demo.CharacterTest
{
    /// <summary>
    /// CharacterTest 场景启动器：驱动 <see cref="GameSlice"/>，传递 Unity 时间，不承载玩法规则。
    /// 与单模块 Demo 不同，模拟帧率与规则层时钟由组合根维护，便于后续接输入 / HUD / 相机 / DebugUI。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("MxFramework/Demo/Character Test/Game Manager")]
    public sealed class GameManager : MonoBehaviour
    {
        [SerializeField] private bool _startPaused;
        [SerializeField] [Min(1)] private int _targetFrameRate = 60;

        private GameSlice _slice;
        private bool _paused;
        private int _previousTargetFrameRate;
        private bool _hasPreviousTargetFrameRate;

        public bool IsPaused => _paused;
        public GameSlice Slice => _slice;

        private void OnEnable()
        {
            ApplyTargetFrameRate();
            _slice = new GameSlice();
            _paused = _startPaused;
        }

        private void OnDisable()
        {
            DisposeSlice();
            RestoreTargetFrameRate();
        }

        private void OnDestroy()
        {
            DisposeSlice();
        }

        private void Update()
        {
            if (_slice == null)
                return;

            if (_paused)
                return;

            double deltaTime = Mathf.Max(0f, Time.deltaTime);
            _slice.Tick(deltaTime);
        }

        public void SetPaused(bool paused)
        {
            _paused = paused;
        }

        public void TogglePaused()
        {
            _paused = !_paused;
        }

        private void DisposeSlice()
        {
            if (_slice == null)
                return;

            _slice.Dispose();
            _slice = null;
        }

        private void ApplyTargetFrameRate()
        {
            if (_hasPreviousTargetFrameRate)
                return;

            _previousTargetFrameRate = Application.targetFrameRate;
            _hasPreviousTargetFrameRate = true;
            Application.targetFrameRate = _targetFrameRate;
        }

        private void RestoreTargetFrameRate()
        {
            if (!_hasPreviousTargetFrameRate)
                return;

            Application.targetFrameRate = _previousTargetFrameRate;
            _hasPreviousTargetFrameRate = false;
        }
    }
}

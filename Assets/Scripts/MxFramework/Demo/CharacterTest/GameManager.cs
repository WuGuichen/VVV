using MxFramework.Runtime;
using MxFramework.Runtime.Unity;
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
        [SerializeField] private bool _logConsole = true;
        [SerializeField] private bool _logConsoleColors = true;
        [SerializeField] private bool _logCategoryColors = true;
        [SerializeField] private string _gameManagerLogColor = "#BB8FCE";
        [SerializeField] private string _gameSliceLogColor = "#58D68D";
        [SerializeField] private string _storyLogColor = "#5DADE2";

        private GameSlice _slice;
        private UnityRuntimeLogger _logger;
        private readonly RuntimeLogBuffer _logBuffer = new RuntimeLogBuffer(96);
        private bool _paused;
        private int _previousTargetFrameRate;
        private bool _hasPreviousTargetFrameRate;

        public bool IsPaused => _paused;
        public GameSlice Slice => _slice;

        private void OnEnable()
        {
            _logger = new UnityRuntimeLogger(this, "CharacterTest")
            {
                Enabled = _logConsole,
                UseRichTextColors = _logConsoleColors
            };
            ConfigureLogColors();
            _logger.Info("GameManager", "OnEnable");

            ApplyTargetFrameRate();
            _slice = new GameSlice(_logger);
            _paused = _startPaused;
            _logBuffer.Clear().Append("GameSlice created. startPaused=").Append(_startPaused);
            _logger.Info("GameManager", _logBuffer);
        }

        private void OnDisable()
        {
            _logger?.Info("GameManager", "OnDisable");
            DisposeSlice();
            RestoreTargetFrameRate();
        }

        private void OnDestroy()
        {
            _logger?.Info("GameManager", "OnDestroy");
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
            _logBuffer.Clear().Append("SetPaused=").Append(_paused);
            _logger?.Info("GameManager", _logBuffer);
        }

        public void TogglePaused()
        {
            _paused = !_paused;
            _logBuffer.Clear().Append("TogglePaused=").Append(_paused);
            _logger?.Info("GameManager", _logBuffer);
        }

        private void DisposeSlice()
        {
            if (_slice == null)
                return;

            _slice.Dispose();
            _slice = null;
            _logger?.Info("GameManager", "GameSlice disposed");
        }

        private void ConfigureLogColors()
        {
            if (_logger == null)
                return;

            _logger.UseCategoryHeaderColors = _logCategoryColors;
            _logger.SetCategoryHeaderColor("GameManager", _gameManagerLogColor);
            _logger.SetCategoryHeaderColor("GameSlice", _gameSliceLogColor);
            _logger.SetCategoryHeaderColor("Story", _storyLogColor);
        }

        private void ApplyTargetFrameRate()
        {
            if (_hasPreviousTargetFrameRate)
                return;

            _previousTargetFrameRate = Application.targetFrameRate;
            _hasPreviousTargetFrameRate = true;
            Application.targetFrameRate = _targetFrameRate;
            _logBuffer.Clear()
                .Append("TargetFrameRate applied. previous=")
                .Append(_previousTargetFrameRate)
                .Append(", current=")
                .Append(_targetFrameRate);
            _logger?.Info("GameManager", _logBuffer);
        }

        private void RestoreTargetFrameRate()
        {
            if (!_hasPreviousTargetFrameRate)
                return;

            Application.targetFrameRate = _previousTargetFrameRate;
            _hasPreviousTargetFrameRate = false;
            _logBuffer.Clear()
                .Append("TargetFrameRate restored. current=")
                .Append(_previousTargetFrameRate);
            _logger?.Info("GameManager", _logBuffer);
        }
    }
}

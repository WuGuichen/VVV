using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MxFramework.Resources.Unity;
using MxFramework.Runtime;
using MxFramework.Runtime.Unity;
using MxFramework.Story.Runtime;
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
        [SerializeField][Min(1)] private int _targetFrameRate = 60;
        [SerializeField] private bool _logConsole = true;
        [SerializeField] private bool _logConsoleColors = true;
        [SerializeField] private bool _logCategoryColors = true;
        [SerializeField] private string _gameManagerLogColor = "#BB8FCE";
        [SerializeField] private string _gameSliceLogColor = "#58D68D";
        [SerializeField] private string _storyLogColor = "#5DADE2";
        [SerializeField] private bool _useExternalStoryAuthoring;
        [SerializeField]
        private string _storyDraftStreamingAssetsPath =
            "MxFramework/CharacterTest/character_test_bootstrap.story.json";
        [SerializeField] private GlobalResourceRuntimeMode _resourceMode = GlobalResourceRuntimeMode.Editor;
        [SerializeField] private string _baseResourcePreloadGroupId = "SpawnCritical";
        [SerializeField] private string _resourcesLogColor = "#F4D03F";

        private const string StoryDebugTargetName = "CharacterTest Story";
        private const string StoryEditorDebugRegistryTypeName =
            "MxFramework.Story.Editor.StoryEditorDebugRegistry, MxFramework.Story.Editor";

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

            ApplyTargetFrameRate();
            CharacterTestStoryContent storyContent = LoadStoryContent();
            GlobalResourceRuntimeServices resources = GlobalResourceRuntimeServices.Create(new GlobalResourceRuntimeOptions
            {
                Mode = _resourceMode,
                AllowMissingPreloadGroupInEditor = true
            });
            LogResourceRuntimeBootstrap(resources);
            _slice = new GameSlice(_logger, storyContent, resources, _baseResourcePreloadGroupId);
            RegisterStoryDebugTarget();
            _paused = _startPaused;
            _logBuffer.Clear().Append("GameSlice created. startPaused=").Append(_startPaused);
            _logger.Info("GameManager", _logBuffer);
        }

        private void OnDisable()
        {
            UnregisterStoryDebugTarget();
            DisposeSlice();
            RestoreTargetFrameRate();
        }

        private void OnDestroy()
        {
            UnregisterStoryDebugTarget();
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

        private CharacterTestStoryContent LoadStoryContent()
        {
            if (!_useExternalStoryAuthoring)
            {
                _logger?.Info("Story", "External Story authoring disabled. Using built-in fixture.");
                return CharacterTestStoryFixture.CreateBootstrapContent();
            }

            string absolutePath = ResolveStreamingAssetsPath(_storyDraftStreamingAssetsPath);
            if (string.IsNullOrEmpty(absolutePath) || !File.Exists(absolutePath))
            {
                _logBuffer.Clear()
                    .Append("Story draft not found. path=")
                    .Append(absolutePath)
                    .Append(". Using built-in fixture.");
                _logger?.Warning("Story", _logBuffer);
                return CharacterTestStoryFixture.CreateBootstrapContent();
            }

            try
            {
                string json = File.ReadAllText(absolutePath);
                if (CharacterTestStoryDraftJson.TryLoad(
                    json,
                    out CharacterTestStoryContent content,
                    out string error,
                    sourcePath: absolutePath))
                {
                    _logBuffer.Clear()
                        .Append("External Story draft loaded. graphId=")
                        .Append(content.GraphId)
                        .Append(", path=")
                        .Append(absolutePath);
                    _logger?.Info("Story", _logBuffer);
                    return content;
                }

                _logBuffer.Clear()
                    .Append(error)
                    .Append(" path=")
                    .Append(absolutePath)
                    .Append(". Using built-in fixture.");
                _logger?.Warning("Story", _logBuffer);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                _logBuffer.Clear()
                    .Append("Story draft read failed: ")
                    .Append(ex.Message)
                    .Append(". Using built-in fixture.");
                _logger?.Warning("Story", _logBuffer);
            }

            return CharacterTestStoryFixture.CreateBootstrapContent();
        }

        private static string ResolveStreamingAssetsPath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return string.Empty;

            string normalizedPath = relativePath.Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(Application.streamingAssetsPath, normalizedPath);
        }

        private void RegisterStoryDebugTarget()
        {
#if UNITY_EDITOR
            if (_slice == null)
                return;

            Type registryType = Type.GetType(StoryEditorDebugRegistryTypeName);
            MethodInfo register = registryType?.GetMethod(
                "Register",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[]
                {
                    typeof(string),
                    typeof(StoryRuntimeModule),
                    typeof(Func<IReadOnlyList<StoryRuntimeEvent>>)
                },
                null);
            if (register == null)
            {
                _logger?.Warning("Story", "Story Runtime Debug registry is unavailable.");
                return;
            }

            Func<IReadOnlyList<StoryRuntimeEvent>> recentEventsProvider =
                () => _slice != null ? _slice.StoryModule.RecentEvents : Array.Empty<StoryRuntimeEvent>();
            register.Invoke(null, new object[] { StoryDebugTargetName, _slice.StoryModule, recentEventsProvider });
            _logger?.Info("Story", "Story Runtime Debug target registered. name=" + StoryDebugTargetName);
#endif
        }

        private void UnregisterStoryDebugTarget()
        {
#if UNITY_EDITOR
            Type registryType = Type.GetType(StoryEditorDebugRegistryTypeName);
            MethodInfo unregister = registryType?.GetMethod(
                "Unregister",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(string) },
                null);
            unregister?.Invoke(null, new object[] { StoryDebugTargetName });
#endif
        }

        private void DisposeSlice()
        {
            if (_slice == null)
                return;

            _slice.Dispose();
            _slice = null;
        }

        private void ConfigureLogColors()
        {
            if (_logger == null)
                return;

            _logger.UseCategoryHeaderColors = _logCategoryColors;
            _logger.SetCategoryHeaderColor("GameManager", _gameManagerLogColor);
            _logger.SetCategoryHeaderColor("GameSlice", _gameSliceLogColor);
            _logger.SetCategoryHeaderColor("Story", _storyLogColor);
            _logger.SetCategoryHeaderColor("Resources", _resourcesLogColor);
        }

        private void LogResourceRuntimeBootstrap(GlobalResourceRuntimeServices resources)
        {
            if (resources == null)
                return;

            _logBuffer.Clear()
                .Append("Global resource runtime initialized. mode=")
                .Append(resources.Mode.ToString())
                .Append(", preloadGroup=")
                .Append(string.IsNullOrWhiteSpace(_baseResourcePreloadGroupId) ? "<none>" : _baseResourcePreloadGroupId)
                .Append(", catalogs=")
                .Append(resources.ResourceManager.CreateDebugSnapshot().CatalogCount);
            _logger?.Info("Resources", _logBuffer);

            if (resources.HasBootstrapError)
            {
                _logBuffer.Clear()
                    .Append("Global resource runtime bootstrap warning: ")
                    .Append(resources.BootstrapErrorMessage);
                _logger?.Warning("Resources", _logBuffer);
            }
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

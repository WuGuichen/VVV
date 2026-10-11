using System;
using System.Collections.Generic;
using System.Reflection;
using MxFramework.DebugUI;
using MxFramework.Runtime;
using MxFramework.Story.Runtime;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>
    /// Wires CharacterTest runtime debug sources to developer-only debug surfaces.
    /// </summary>
    public sealed class CharacterTestDebugBootstrap
    {
        private const string StoryDebugTargetName = "CharacterTest Story";
        private const string RuntimeAiPlannerDebugTargetName = "CharacterTest Runtime AI Planner";
        private const string StoryEditorDebugRegistryTypeName =
            "MxFramework.Story.Editor.StoryEditorDebugRegistry, MxFramework.Story.Editor";
        private const string RuntimeAiPlannerEditorDebugRegistryTypeName =
            "MxFramework.Game.CharacterTest.Editor.CharacterTestRuntimeAiPlannerEditorDebugRegistry, MxFramework.Game.CharacterTest.Editor";

        private readonly MonoBehaviour _owner;
        private readonly IRuntimeLogger _logger;
        private CharacterTestDebugUiHost _debugUiHost;

        public CharacterTestDebugBootstrap(MonoBehaviour owner, IRuntimeLogger logger)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _logger = logger;
        }

        public bool IsDebugUiConfigured => _debugUiHost != null;

        public void ConfigureDebugUi(
            GameSlice slice,
            Func<bool> isPausedProvider,
            PanelSettings panelSettings,
            DebugUiVisibility initialVisibility,
            bool showOnStart,
            int initialTabIndex)
        {
            CharacterTestSliceServices services = slice?.Services;
            if (services == null)
                return;

            _debugUiHost = _owner.GetComponent<CharacterTestDebugUiHost>();
            if (_debugUiHost == null)
                _debugUiHost = _owner.gameObject.AddComponent<CharacterTestDebugUiHost>();

            _debugUiHost.Configure(
                services,
                isPausedProvider,
                panelSettings,
                initialVisibility,
                showOnStart,
                initialTabIndex,
                _logger);
            _logger?.Info("GameManager", "CharacterTest developer debug panel configured.");
        }

        public void RegisterStoryDebugTarget(GameSlice slice)
        {
#if UNITY_EDITOR
            CharacterTestSliceServices services = slice?.Services;
            if (services == null)
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
                () => services.Modules.StoryModule.RecentEvents;
            register.Invoke(
                null,
                new object[] { StoryDebugTargetName, services.Modules.StoryModule, recentEventsProvider });
            _logger?.Info("Story", "Story Runtime Debug target registered. name=" + StoryDebugTargetName);
#endif
        }

        internal void ConfigureGuidanceSelection(
            Func<CharacterTestGuidanceSelectionSnapshot> provider,
            Action<string> sink)
        {
            _debugUiHost?.Panel?.ConfigureGuidanceSelection(provider, sink);
        }

        public void RegisterRuntimeAiPlannerDebugTarget(
            GameSlice slice,
            Func<bool> runtimeAiBehaviorSelectionUnlockedProvider = null,
            Action<bool> setRuntimeAiBehaviorSelectionUnlocked = null,
            Func<bool> openRuntimeAiBehaviorLoadoutPanel = null,
            Func<bool> closeRuntimeAiBehaviorLoadoutPanel = null,
            Func<bool> runtimeAiBehaviorLoadoutPanelOpenProvider = null,
            Func<string> runtimeAiBehaviorLoadoutPanelStatusProvider = null)
        {
#if UNITY_EDITOR
            CharacterTestSliceServices services = slice?.Services;
            CharacterTestRuntimeAiPlannerCommandModule module = services?.Modules.RuntimeAiPlannerCommands;
            if (module == null)
                return;

            Type registryType = Type.GetType(RuntimeAiPlannerEditorDebugRegistryTypeName);
            MethodInfo register = registryType?.GetMethod(
                "Register",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[]
                {
                    typeof(string),
                    typeof(Func<CharacterTestRuntimeAiPlannerSnapshot>),
                    typeof(Func<object, int, object>),
                    typeof(Func<bool>),
                    typeof(Action<bool>),
                    typeof(Func<bool>),
                    typeof(Func<bool>),
                    typeof(Func<bool>),
                    typeof(Func<string>)
                },
                null);
            if (register == null)
            {
                _logger?.Warning("CharacterTest.RuntimeAiPlanner", "Runtime AI Planner Debug registry is unavailable.");
                return;
            }

            Func<CharacterTestRuntimeAiPlannerSnapshot> snapshotProvider = module.CreateSnapshot;
            Func<object, int, object> behaviorCompositionHotUpdateProvider =
                (composition, profileSlot) =>
                {
                    if (!(composition is CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile))
                    {
                        return new CharacterTestRuntimeAiPlannerBehaviorHotUpdateResult(
                            false,
                            string.Empty,
                            profileSlot,
                            "Runtime AI Planner behavior composition hot update payload is invalid.");
                    }

                    module.TryApplyBehaviorCompositionOverlay(
                        profile,
                        profileSlot,
                        out CharacterTestRuntimeAiPlannerBehaviorHotUpdateResult result);
                    return result;
                };
            register.Invoke(
                null,
                new object[]
                {
                    RuntimeAiPlannerDebugTargetName,
                    snapshotProvider,
                    behaviorCompositionHotUpdateProvider,
                    runtimeAiBehaviorSelectionUnlockedProvider,
                    setRuntimeAiBehaviorSelectionUnlocked,
                    openRuntimeAiBehaviorLoadoutPanel,
                    closeRuntimeAiBehaviorLoadoutPanel,
                    runtimeAiBehaviorLoadoutPanelOpenProvider,
                    runtimeAiBehaviorLoadoutPanelStatusProvider
                });
            _logger?.Info(
                "CharacterTest.RuntimeAiPlanner",
                "Runtime AI Planner Debug target registered. name=" + RuntimeAiPlannerDebugTargetName);
#endif
        }

        public void Release()
        {
            ReleaseDebugUi();
            UnregisterStoryDebugTarget();
            UnregisterRuntimeAiPlannerDebugTarget();
        }

        private void ReleaseDebugUi()
        {
            if (_debugUiHost == null)
                return;

            _debugUiHost.Release();
            _debugUiHost = null;
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

        private void UnregisterRuntimeAiPlannerDebugTarget()
        {
#if UNITY_EDITOR
            Type registryType = Type.GetType(RuntimeAiPlannerEditorDebugRegistryTypeName);
            MethodInfo unregister = registryType?.GetMethod(
                "Unregister",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(string) },
                null);
            unregister?.Invoke(null, new object[] { RuntimeAiPlannerDebugTargetName });
#endif
        }
    }
}

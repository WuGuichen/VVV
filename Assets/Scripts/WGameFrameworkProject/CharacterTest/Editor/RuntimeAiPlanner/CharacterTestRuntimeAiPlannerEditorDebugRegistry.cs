using System;
using System.Collections.Generic;

namespace MxFramework.Game.CharacterTest.Editor
{
    public sealed class CharacterTestRuntimeAiPlannerEditorDebugTarget
    {
        public CharacterTestRuntimeAiPlannerEditorDebugTarget(
            string name,
            Func<CharacterTestRuntimeAiPlannerSnapshot> snapshotProvider)
            : this(name, snapshotProvider, null)
        {
        }

        public CharacterTestRuntimeAiPlannerEditorDebugTarget(
            string name,
            Func<CharacterTestRuntimeAiPlannerSnapshot> snapshotProvider,
            Func<object, int, object> behaviorCompositionHotUpdateProvider)
            : this(
                name,
                snapshotProvider,
                behaviorCompositionHotUpdateProvider,
                null,
                null,
                null,
                null,
                null,
                null)
        {
        }

        public CharacterTestRuntimeAiPlannerEditorDebugTarget(
            string name,
            Func<CharacterTestRuntimeAiPlannerSnapshot> snapshotProvider,
            Func<object, int, object> behaviorCompositionHotUpdateProvider,
            Func<bool> runtimeAiBehaviorSelectionUnlockedProvider,
            Action<bool> setRuntimeAiBehaviorSelectionUnlocked,
            Func<bool> openRuntimeAiBehaviorLoadoutPanel,
            Func<bool> closeRuntimeAiBehaviorLoadoutPanel,
            Func<bool> runtimeAiBehaviorLoadoutPanelOpenProvider,
            Func<string> runtimeAiBehaviorLoadoutPanelStatusProvider)
        {
            Name = string.IsNullOrWhiteSpace(name)
                ? CharacterTestRuntimeAiPlannerDebugSource.DefaultSourceName
                : name;
            SnapshotProvider = snapshotProvider;
            _behaviorCompositionHotUpdateProvider = behaviorCompositionHotUpdateProvider;
            _runtimeAiBehaviorSelectionUnlockedProvider = runtimeAiBehaviorSelectionUnlockedProvider;
            _setRuntimeAiBehaviorSelectionUnlocked = setRuntimeAiBehaviorSelectionUnlocked;
            _openRuntimeAiBehaviorLoadoutPanel = openRuntimeAiBehaviorLoadoutPanel;
            _closeRuntimeAiBehaviorLoadoutPanel = closeRuntimeAiBehaviorLoadoutPanel;
            _runtimeAiBehaviorLoadoutPanelOpenProvider = runtimeAiBehaviorLoadoutPanelOpenProvider;
            _runtimeAiBehaviorLoadoutPanelStatusProvider = runtimeAiBehaviorLoadoutPanelStatusProvider;
        }

        private readonly Func<object, int, object> _behaviorCompositionHotUpdateProvider;
        private readonly Func<bool> _runtimeAiBehaviorSelectionUnlockedProvider;
        private readonly Action<bool> _setRuntimeAiBehaviorSelectionUnlocked;
        private readonly Func<bool> _openRuntimeAiBehaviorLoadoutPanel;
        private readonly Func<bool> _closeRuntimeAiBehaviorLoadoutPanel;
        private readonly Func<bool> _runtimeAiBehaviorLoadoutPanelOpenProvider;
        private readonly Func<string> _runtimeAiBehaviorLoadoutPanelStatusProvider;

        public string Name { get; }
        public Func<CharacterTestRuntimeAiPlannerSnapshot> SnapshotProvider { get; }
        public bool IsAvailable => SnapshotProvider != null;
        internal bool CanApplyBehaviorCompositionOverlay => _behaviorCompositionHotUpdateProvider != null;
        internal bool HasRuntimeAiBehaviorSelectionControls =>
            _runtimeAiBehaviorSelectionUnlockedProvider != null
            && _setRuntimeAiBehaviorSelectionUnlocked != null;
        internal bool CanOpenRuntimeAiBehaviorLoadoutPanel => _openRuntimeAiBehaviorLoadoutPanel != null;
        internal bool CanCloseRuntimeAiBehaviorLoadoutPanel => _closeRuntimeAiBehaviorLoadoutPanel != null;
        internal bool RuntimeAiBehaviorLoadoutPanelOpen =>
            _runtimeAiBehaviorLoadoutPanelOpenProvider != null
            && _runtimeAiBehaviorLoadoutPanelOpenProvider();
        internal string RuntimeAiBehaviorLoadoutPanelStatus =>
            _runtimeAiBehaviorLoadoutPanelStatusProvider != null
                ? _runtimeAiBehaviorLoadoutPanelStatusProvider() ?? string.Empty
                : string.Empty;
        internal bool RuntimeAiBehaviorSelectionUnlocked =>
            _runtimeAiBehaviorSelectionUnlockedProvider != null
            && _runtimeAiBehaviorSelectionUnlockedProvider();

        internal void SetRuntimeAiBehaviorSelectionUnlocked(bool unlocked)
        {
            _setRuntimeAiBehaviorSelectionUnlocked?.Invoke(unlocked);
        }

        internal bool OpenRuntimeAiBehaviorLoadoutPanel()
        {
            return _openRuntimeAiBehaviorLoadoutPanel != null
                && _openRuntimeAiBehaviorLoadoutPanel();
        }

        internal bool CloseRuntimeAiBehaviorLoadoutPanel()
        {
            return _closeRuntimeAiBehaviorLoadoutPanel != null
                && _closeRuntimeAiBehaviorLoadoutPanel();
        }

        internal bool TryApplyBehaviorCompositionOverlay(
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile,
            int profileSlot,
            out CharacterTestRuntimeAiPlannerBehaviorHotUpdateResult result)
        {
            result = default;
            if (_behaviorCompositionHotUpdateProvider == null)
                return false;

            object raw = _behaviorCompositionHotUpdateProvider(profile, profileSlot);
            if (raw is CharacterTestRuntimeAiPlannerBehaviorHotUpdateResult typed)
            {
                result = typed;
                return typed.Success;
            }

            result = new CharacterTestRuntimeAiPlannerBehaviorHotUpdateResult(
                false,
                profile != null ? profile.CompositionId : string.Empty,
                profileSlot,
                "Runtime AI Planner behavior composition hot update returned an invalid result.");
            return false;
        }
    }

    public static class CharacterTestRuntimeAiPlannerEditorDebugRegistry
    {
        private static readonly List<CharacterTestRuntimeAiPlannerEditorDebugTarget> TargetsInternal =
            new List<CharacterTestRuntimeAiPlannerEditorDebugTarget>();

        public static IReadOnlyList<CharacterTestRuntimeAiPlannerEditorDebugTarget> Targets => TargetsInternal;

        public static CharacterTestRuntimeAiPlannerEditorDebugTarget Register(
            string name,
            Func<CharacterTestRuntimeAiPlannerSnapshot> snapshotProvider)
        {
            return Register(name, snapshotProvider, null);
        }

        public static CharacterTestRuntimeAiPlannerEditorDebugTarget Register(
            string name,
            Func<CharacterTestRuntimeAiPlannerSnapshot> snapshotProvider,
            Func<object, int, object> behaviorCompositionHotUpdateProvider)
        {
            return Register(
                name,
                snapshotProvider,
                behaviorCompositionHotUpdateProvider,
                null,
                null,
                null,
                null,
                null,
                null);
        }

        public static CharacterTestRuntimeAiPlannerEditorDebugTarget Register(
            string name,
            Func<CharacterTestRuntimeAiPlannerSnapshot> snapshotProvider,
            Func<object, int, object> behaviorCompositionHotUpdateProvider,
            Func<bool> runtimeAiBehaviorSelectionUnlockedProvider,
            Action<bool> setRuntimeAiBehaviorSelectionUnlocked,
            Func<bool> openRuntimeAiBehaviorLoadoutPanel,
            Func<bool> closeRuntimeAiBehaviorLoadoutPanel,
            Func<bool> runtimeAiBehaviorLoadoutPanelOpenProvider,
            Func<string> runtimeAiBehaviorLoadoutPanelStatusProvider)
        {
            var target = new CharacterTestRuntimeAiPlannerEditorDebugTarget(
                name,
                snapshotProvider,
                behaviorCompositionHotUpdateProvider,
                runtimeAiBehaviorSelectionUnlockedProvider,
                setRuntimeAiBehaviorSelectionUnlocked,
                openRuntimeAiBehaviorLoadoutPanel,
                closeRuntimeAiBehaviorLoadoutPanel,
                runtimeAiBehaviorLoadoutPanelOpenProvider,
                runtimeAiBehaviorLoadoutPanelStatusProvider);
            for (int i = 0; i < TargetsInternal.Count; i++)
            {
                if (string.Equals(TargetsInternal[i].Name, target.Name, StringComparison.Ordinal))
                {
                    TargetsInternal[i] = target;
                    return target;
                }
            }

            TargetsInternal.Add(target);
            return target;
        }

        public static bool Unregister(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            for (int i = 0; i < TargetsInternal.Count; i++)
            {
                if (!string.Equals(TargetsInternal[i].Name, name, StringComparison.Ordinal))
                    continue;

                TargetsInternal.RemoveAt(i);
                return true;
            }

            return false;
        }

        public static void Clear()
        {
            TargetsInternal.Clear();
        }
    }
}

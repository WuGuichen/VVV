using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    public sealed partial class CharacterTestRuntimeAiPlannerWindow
    {
        private const string BehaviorGroupPreviewCandidateCurrentDraftKey = "current-draft";
        private const string BehaviorGroupPreviewCandidateBuiltInDefaultKey = "built-in-default";
        private const string BehaviorGroupPreviewCandidateDefaultLoadoutVariantKey = "default-loadout-variant";

        private string _behaviorDefinitionSearchText = string.Empty;
        private int _selectedBehaviorGroupEntryIndex = -1;

        private static bool BehaviorHotUpdateEnabled
        {
            get => EditorPrefs.GetBool(BehaviorHotUpdatePrefKey, false);
            set => EditorPrefs.SetBool(BehaviorHotUpdatePrefKey, value);
        }

        private static bool BehaviorGroupGuideExpanded
        {
            get => EditorPrefs.GetBool(BehaviorGroupGuideFoldoutPrefKey, false);
            set => EditorPrefs.SetBool(BehaviorGroupGuideFoldoutPrefKey, value);
        }

        private static bool BehaviorGroupEditExpanded
        {
            get => EditorPrefs.GetBool(BehaviorGroupEditFoldoutPrefKey, false);
            set => EditorPrefs.SetBool(BehaviorGroupEditFoldoutPrefKey, value);
        }
    }
}

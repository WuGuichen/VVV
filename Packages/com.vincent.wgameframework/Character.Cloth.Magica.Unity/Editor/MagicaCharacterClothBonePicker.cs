using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MxFramework.CharacterCloth.Magica.Unity.Editor
{
    internal sealed class MagicaCharacterClothBonePicker : PopupWindowContent
    {
        private readonly IReadOnlyList<string> _paths;
        private readonly Action<string> _selected;
        private string _search = string.Empty;
        private Vector2 _scroll;

        internal MagicaCharacterClothBonePicker(IReadOnlyList<string> paths, Action<string> selected)
        {
            _paths = paths ?? Array.Empty<string>();
            _selected = selected;
        }

        public override Vector2 GetWindowSize() => new Vector2(520f, 430f);

        public override void OnGUI(Rect rect)
        {
            EditorGUILayout.LabelField("选择 BodyModel 骨骼", EditorStyles.boldLabel);
            GUI.SetNextControlName("BoneSearch");
            _search = EditorGUILayout.TextField("搜索", _search ?? string.Empty);
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                editorWindow.Close();
                Event.current.Use();
                return;
            }
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            int shown = 0;
            for (int i = 0; i < _paths.Count; i++)
            {
                string path = _paths[i] ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(_search)
                    && path.IndexOf(_search, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                shown++;
                if (GUILayout.Button(path, EditorStyles.miniButton, GUILayout.ExpandWidth(true)))
                {
                    _selected?.Invoke(path);
                    editorWindow.Close();
                    GUIUtility.ExitGUI();
                }
            }
            if (shown == 0) EditorGUILayout.HelpBox("没有匹配骨骼。", MessageType.Info);
            EditorGUILayout.EndScrollView();
            if (Event.current.type == EventType.Repaint && string.IsNullOrWhiteSpace(_search))
                EditorGUI.FocusTextInControl("BoneSearch");
        }
    }
}

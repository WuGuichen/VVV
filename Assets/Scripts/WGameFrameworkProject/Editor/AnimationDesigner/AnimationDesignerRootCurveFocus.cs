using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Project.Editor.AnimationDesigner
{
    /// <summary>Unity 6000.5 Animation window hierarchy bridge for the three Root T curves.</summary>
    internal static class AnimationDesignerRootCurveFocus
    {
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        internal static bool HasRootTranslationCurves(AnimationClip clip)
        {
            if (clip == null) return false;
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
                if (IsRootTranslation(binding.propertyName)) return true;
            return false;
        }

        internal static bool TryFocus(AnimationWindow window, AnimationClip clip, out string diagnostic)
        {
            diagnostic = string.Empty;
            if (window == null || clip == null || window.animationClip != clip)
            { diagnostic = "Animation 窗口当前不是所选片段。"; return false; }
            try
            {
                var expected = new HashSet<string>(StringComparer.Ordinal);
                foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
                    if (IsRootTranslation(binding.propertyName)) expected.Add(binding.propertyName);
                if (expected.Count == 0)
                { diagnostic = "片段没有 Root T.x/y/z 曲线。"; return false; }

                object state = typeof(AnimationWindow).GetProperty("state", Members)?.GetValue(window);
                object hierarchy = state?.GetType().GetField("hierarchyData", Members)?.GetValue(state);
                object root = hierarchy?.GetType().GetProperty("root", Members)?.GetValue(hierarchy);
                if (root == null)
                { diagnostic = "Animation 窗口曲线树尚未就绪。"; return false; }
                if (!FindRootTranslationGroup(root, expected, out int groupId, out int[] curveIds))
                { diagnostic = "曲线树中尚未找到 Root T.x/y/z。"; return false; }

                Type hierarchyType = hierarchy.GetType();
                MethodInfo expand = hierarchyType.GetMethod("SetExpanded", Members, null, new[] { typeof(int), typeof(bool) }, null);
                MethodInfo reveal = hierarchyType.GetMethod("RevealItems", Members, null, new[] { typeof(int[]) }, null);
                MethodInfo select = state.GetType().GetMethod("SelectHierarchyItems", Members, null,
                    new[] { typeof(IEnumerable<int>), typeof(bool), typeof(bool) }, null);
                FieldInfo curvesTab = state.GetType().GetField("showCurveEditor", Members);
                if (expand == null || reveal == null || select == null || curvesTab == null)
                { diagnostic = "当前 Unity 版本的曲线定位接口不兼容。"; return false; }
                expand.Invoke(hierarchy, new object[] { groupId, true });
                reveal.Invoke(hierarchy, new object[] { curveIds });
                select.Invoke(state, new object[] { curveIds, false, false });
                curvesTab.SetValue(state, true);
                window.Repaint();
                return true;
            }
            catch (Exception ex)
            {
                diagnostic = ex.GetBaseException().Message;
                return false;
            }
        }

        private static bool FindRootTranslationGroup(object node, HashSet<string> expected, out int groupId, out int[] curveIds)
        {
            groupId = 0;
            curveIds = null;
            IList children = node.GetType().GetProperty("children", Members)?.GetValue(node) as IList;
            if (children == null) return false;
            var matches = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (object child in children)
            {
                object value = child.GetType().GetField("binding", Members)?.GetValue(child);
                if (value is EditorCurveBinding binding && expected.Contains(binding.propertyName))
                    matches[binding.propertyName] = NodeId(child);
            }
            if (matches.Count == expected.Count)
            {
                groupId = NodeId(node);
                var ids = new List<int>(3);
                foreach (string axis in new[] { "RootT.x", "RootT.y", "RootT.z" })
                    if (matches.TryGetValue(axis, out int id)) ids.Add(id);
                curveIds = ids.ToArray();
                return true;
            }
            foreach (object child in children)
                if (FindRootTranslationGroup(child, expected, out groupId, out curveIds)) return true;
            return false;
        }

        private static int NodeId(object node) => (int)node.GetType().GetProperty("id", Members).GetValue(node);

        private static bool IsRootTranslation(string propertyName) => propertyName == "RootT.x"
            || propertyName == "RootT.y" || propertyName == "RootT.z";
    }
}

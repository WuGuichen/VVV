using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Project.Editor.AnimationDesigner
{
    internal static class AnimationDesignerClipSave
    {
        internal static bool CanSave(AnimationClip clip)
        {
            if (clip == null || AssetDatabase.IsSubAsset(clip)) return false;
            string path = AssetDatabase.GetAssetPath(clip);
            return path.StartsWith("Assets/", StringComparison.Ordinal)
                && path.EndsWith(".anim", StringComparison.OrdinalIgnoreCase);
        }

        internal static string Save(AnimationClip clip)
        {
            if (!CanSave(clip)) return "请选择 Assets 下独立的 .anim；FBX 子片段需要先导出。";
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                return "请退出 Play Mode 并等待 Editor 就绪后保存动画。";
            if (AnimationClipReplacementService.Busy)
                return "动画替换或撤回仍在进行，请等待操作结束。";
            foreach (AnimationWindow window in Resources.FindObjectsOfTypeAll<AnimationWindow>())
                if (window.animationClip == clip && window.recording)
                    return "请先停止 Animation 窗口的录制，再保存当前动画。";
            if (!EditorUtility.IsDirty(clip)) return "当前动画已保存，没有需要写入的修改。";

            string path = AssetDatabase.GetAssetPath(clip);
            string metaPath = path + ".meta";
            if (!File.Exists(path) || !File.Exists(metaPath)) return "动画或 .meta 文件不存在，未执行保存。";
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(clip, out string guid, out long localId))
                return "无法读取动画资源身份，未执行保存。";
            byte[] assetBefore = File.ReadAllBytes(path);
            byte[] metaBefore = File.ReadAllBytes(metaPath);
            try
            {
                AssetDatabase.SaveAssetIfDirty(clip);
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(clip, out string savedGuid, out long savedId)
                    || savedGuid != guid || savedId != localId || !metaBefore.SequenceEqual(File.ReadAllBytes(metaPath)))
                    return "动画已写入，但资源身份或 .meta 发生变化，请检查该片段。";
                if (EditorUtility.IsDirty(clip))
                    return assetBefore.SequenceEqual(File.ReadAllBytes(path))
                        ? "保存后动画仍有未保存修改，且未确认写入磁盘；请重试并检查文件权限。"
                        : "动画已写入磁盘，但编辑器又检测到未保存修改；请继续检查后再次保存。";
                return "已保存 " + Path.GetFileName(path) + "；仅写入这个动画，未保存其他资源或创建 Git 提交。";
            }
            catch (Exception ex)
            {
                return "保存动画失败：" + ex.GetBaseException().Message;
            }
        }
    }
}

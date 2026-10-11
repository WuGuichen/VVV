using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MxFramework.Project.Editor.AnimationDesigner
{
    // All knowledge of FImpossible's un-asmdef'd editor assembly belongs here.
    internal sealed class AnimationDesignerAdapter
    {
        private const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private readonly Type _type;
        private EditorWindow _window;
        private Animator _ownedAnimator;
        private readonly Dictionary<Animator, ScriptableObject> _temporarySaves = new Dictionary<Animator, ScriptableObject>();
        internal bool IsOpen => _window != null;
        internal Animator BoundAnimator => _window != null ? (Animator)Get("currentMecanim") : null;
        internal AnimationClip Clip => _window != null ? (AnimationClip)Get("TargetClip") : null;
        internal AnimationClip LatestExport => _window != null ? Get("LatestSaved") as AnimationClip : null;
        internal string CaptureSetup() => _window != null && Get("DisplaySave") is ScriptableObject save
            ? EditorJsonUtility.ToJson(save, true) : null;
        internal void SetInteractionEnabled(bool enabled)
        {
            if (_window != null) _window.rootVisualElement.SetEnabled(enabled);
        }

        private AnimationDesignerAdapter(Type type) { _type = type; }

        internal static bool TryCreate(out AnimationDesignerAdapter adapter, out string diagnostic)
        {
            adapter = null;
            diagnostic = string.Empty;
            Type type = Type.GetType("FIMSpace.AnimationTools.AnimationDesignerWindow, Assembly-CSharp-Editor");
            if (type == null) { diagnostic = "未找到 Animation Designer 插件。"; return false; }
            foreach (string name in new[] { "TargetClip", "LatestSaved", "_latestClip", "currentMecanim", "latestAnimator", "originalAnimatorController", "ProjectFileSave", "TempSave", "Temps", "DisplaySave", "_latest_DisplaySave", "playPreview", "_serializationChanges", "playablePreview", "playableMorphPreviews", "playableSecondaryAnimatorPreview" })
                if (type.GetField(name, Members) == null) { diagnostic = "Animation Designer 版本不兼容：缺少 " + name; return false; }
            foreach (string name in new[] { "Init", "AddDampReferencesEvent", "UseEditorEvents", "GetTempSaveFor", "_LimbsRefresh" })
                if (type.GetMethod(name, Members) == null) { diagnostic = "Animation Designer 版本不兼容：缺少 " + name; return false; }
            Type saveType = type.GetField("DisplaySave", Members).FieldType;
            if (saveType.GetMethod("DampSessionSkeletonReferences", Members) == null
                || saveType.GetMethod("GetArmature", Members) == null || saveType.GetMethod("RefreshSkeleton", Members) == null
                || saveType.GetField("Armature", Members) == null || saveType.GetField("TPose", Members) == null
                || saveType.GetMethod("GatherBones", Members) == null || saveType.GetProperty("LatestAnimator", Members) == null)
            { diagnostic = "Animation Designer 存档接口不兼容。"; return false; }
            adapter = new AnimationDesignerAdapter(type);
            adapter._window = type.GetField("Get", Members)?.GetValue(null) as EditorWindow;
            return true;
        }

        internal static bool ValidateClip(Animator animator, AnimationClip clip, out string diagnostic)
        {
            diagnostic = string.Empty;
            if (clip == null) { diagnostic = "请选择动画片段。"; return false; }
            if (clip.isHumanMotion && !animator.isHuman)
            { diagnostic = "此 Humanoid 动画需要 Humanoid 角色。"; return false; }
            if (!clip.isHumanMotion)
            {
                var curves = AnimationUtility.GetCurveBindings(clip);
                int transformCurves = 0, matching = 0;
                foreach (var curve in curves)
                {
                    if (curve.type != typeof(Transform)) continue;
                    transformCurves++;
                    if (string.IsNullOrEmpty(curve.path) || animator.transform.Find(curve.path) != null) matching++;
                }
                if (transformCurves > 0 && matching == 0)
                { diagnostic = "动画骨骼路径与所选角色不匹配。"; return false; }
            }
            return true;
        }

        internal void Attach(Animator animator, AnimationClip clip)
        {
            if (!SupportsScene(animator))
                throw new InvalidOperationException("Animation Designer 需要普通场景中的临时模型，不支持 PreviewScene 或 Prefab 隔离场景。");
            if (_window == null) { Invoke("Init"); _window = (EditorWindow)_type.GetField("Get", Members).GetValue(null); }
            if (BoundAnimator != animator)
            {
                Detach();
                Invoke("AddDampReferencesEvent", false);
                Invoke("UseEditorEvents");
                var save = (ScriptableObject)Invoke("GetTempSaveFor", animator.transform);
                _temporarySaves[animator] = save;
                _ownedAnimator = animator;
                Set("TempSave", save); Set("DisplaySave", save); Set("_latest_DisplaySave", save);
                Set("latestAnimator", animator.transform); Set("currentMecanim", animator);
                Set("originalAnimatorController", animator.runtimeAnimatorController);
                Type saveType = save.GetType();
                saveType.GetProperty("LatestAnimator", Members).SetValue(save, animator.transform);
                FieldInfo armatureField = saveType.GetField("Armature", Members);
                bool fresh = armatureField.GetValue(save) == null;
                if (fresh)
                {
                    armatureField.SetValue(save, Activator.CreateInstance(armatureField.FieldType));
                    FieldInfo poseField = saveType.GetField("TPose", Members);
                    poseField.SetValue(save, Activator.CreateInstance(poseField.FieldType));
                    saveType.GetField("TargetAvatar", Members).SetValue(save, animator.avatar);
                    saveType.GetMethod("RefreshSkeleton", Members).Invoke(save, new object[] { animator.transform });
                    saveType.GetMethod("GetArmature", Members).Invoke(save, new object[] { false });
                }
                saveType.GetMethod("GatherBones", Members).Invoke(save, null);
                if (fresh) Invoke("_LimbsRefresh");
                Set("_serializationChanges", true);
            }
            _ownedAnimator = animator;
            Set("playPreview", false);
            Set("TargetClip", clip);
            Selection.activeGameObject = animator.gameObject;
            _window.Show(); _window.Focus(); _window.Repaint();
        }

        internal static bool SupportsScene(Animator animator) => animator != null
            && !EditorUtility.IsPersistent(animator) && animator.gameObject.scene.IsValid()
            && !UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(animator.gameObject.scene);

        internal bool ConfirmRelease()
        {
            if (_window == null) return true;
            var save = Get("DisplaySave") as ScriptableObject;
            if (save == null) return true;
            bool persistent = AssetDatabase.Contains(save);
            if (persistent && !EditorUtility.IsDirty(save)) return true;
            int answer = EditorUtility.DisplayDialogComplex("保存 Animation Designer 设置？",
                "当前动画设计设置尚未保存。可以保存插件设置文件后继续，或放弃这些设置。动画片段导出仍在 Animation Designer 中操作。",
                "保存设置…", "取消", "放弃并继续");
            if (answer == 1) return false;
            if (answer == 2) return true;
            if (persistent) { AssetDatabase.SaveAssetIfDirty(save); return true; }
            string path = EditorUtility.SaveFilePanelInProject("保存 Animation Designer 设置", "AnimationDesigner_Setup", "asset", "保存插件原生设置文件");
            if (string.IsNullOrEmpty(path)) return false;
            SaveTemporarySetup(save, path);
            return true;
        }

        private void SaveTemporarySetup(ScriptableObject save, string path)
        {
            // Same native save format as the plugin's New Save button; transient scene references are detached on the copy.
            var copy = Object.Instantiate(save);
            copy.GetType().GetMethod("DampSessionSkeletonReferences", Members).Invoke(copy, new object[] { true });
            var serialized = new SerializedObject(copy);
            var property = serialized.GetIterator();
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                Object reference = property.objectReferenceValue;
                if ((reference is GameObject || reference is Component) && !EditorUtility.IsPersistent(reference))
                    property.objectReferenceValue = null;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            serialized.Dispose();
            AssetDatabase.CreateAsset(copy, AssetDatabase.GenerateUniqueAssetPath(path));
            AssetDatabase.SaveAssetIfDirty(copy);
            Set("ProjectFileSave", copy);
        }

        internal void Detach()
        {
            DisposeOwnedPlayables();
            if (_window == null || _ownedAnimator == null || BoundAnimator != _ownedAnimator) { _ownedAnimator = null; return; }
            Set("playPreview", false);
            Invoke("AddDampReferencesEvent", false);
            Invoke("UseEditorEvents");
            // OnGUI compares this cache before testing whether a setup exists. Clear both to avoid a clip-change callback into null S.
            Set("TargetClip", null); Set("_latestClip", null); Set("currentMecanim", null); Set("TempSave", null); Set("DisplaySave", null);
            _ownedAnimator = null;
            _window.Repaint();
        }

        private void DisposeOwnedPlayables()
        {
            if (_ownedAnimator == null) return;
            foreach (string name in new[] { "playablePreview", "playableSecondaryAnimatorPreview" })
            {
                object playable = Get(name);
                if (DisposeIfOwned(playable)) Set(name, null);
            }
            if (Get("playableMorphPreviews") is IList list)
                for (int i = list.Count - 1; i >= 0; i--)
                    if (DisposeIfOwned(list[i])) list.RemoveAt(i);
        }

        internal void ReleaseTemporarySetup(Animator animator)
        {
            if (animator == null) return;
            if (_window != null && _type.GetField("Temps", Members)?.GetValue(_window) is IList list)
                for (int i = list.Count - 1; i >= 0; i--)
                    if (list[i].GetType().GetField("Anim").GetValue(list[i]) as Transform == animator.transform) list.RemoveAt(i);
            if (_temporarySaves.TryGetValue(animator, out ScriptableObject save))
            {
                _temporarySaves.Remove(animator);
                if (save != null && !AssetDatabase.Contains(save)) Object.DestroyImmediate(save);
            }
        }

        private bool DisposeIfOwned(object playable)
        {
            if (playable == null || playable.GetType().GetField("characterAnimator")?.GetValue(playable) as Animator != _ownedAnimator) return false;
            playable.GetType().GetMethod("Dispose")?.Invoke(playable, null);
            return true;
        }

        private object Get(string name) => _type.GetField(name, Members).GetValue(_window);
        private void Set(string name, object value) => _type.GetField(name, Members).SetValue(_window, value);
        private object Invoke(string name, params object[] args) => _type.GetMethod(name, Members).Invoke(_window, args);
    }
}

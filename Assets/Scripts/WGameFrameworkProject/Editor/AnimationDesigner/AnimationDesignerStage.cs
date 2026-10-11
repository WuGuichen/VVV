using System;
using MxFramework.Editor.CharacterPreview;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MxFramework.Project.Editor.AnimationDesigner
{
    internal sealed class AnimationDesignerStage : PreviewSceneStage
    {
        private Scene _previousActiveScene;
        protected override GUIContent CreateHeaderContent() => new GUIContent("Animation Designer · 临时角色");
        internal static AnimationDesignerStage Create()
        {
            // Reuse stage navigation and its isolated SceneView/Hierarchy, but not its preview-scene allocation:
            // Animation Designer explicitly refuses every scene flagged as a PreviewScene in OnGUI.
            Scene previousActiveScene = SceneManager.GetActiveScene();
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path))
                    throw new InvalidOperationException("当前包含未命名场景，请先为它保存路径，再打开临时动画编辑。已有命名场景的未保存修改不会被保存或丢弃。");
            Scene editingScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            editingScene.name = "Animation Designer (Temporary)";
            EditorSceneManager.SetSceneCullingMask(editingScene, EditorSceneManager.CalculateAvailableSceneCullingMask());
            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                SceneManager.SetActiveScene(previousActiveScene);
            var stage = CreateInstance<AnimationDesignerStage>();
            stage._previousActiveScene = previousActiveScene;
            stage.scene = editingScene;
            return stage;
        }
        // NewScene changes Unity's stage navigation. Allocate before GoToStage, never inside OnOpenStage.
        protected override bool OnOpenStage() => scene.IsValid() && scene.isLoaded;
        protected override void OnCloseStage()
        {
            AnimationDesignerSession.StageClosed(this);
            if (scene.IsValid())
            {
                if (SceneManager.GetActiveScene() == scene && _previousActiveScene.IsValid() && _previousActiveScene.isLoaded)
                    SceneManager.SetActiveScene(_previousActiveScene);
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    [InitializeOnLoad]
    internal static partial class AnimationDesignerSession
    {
        private static AnimationDesignerStage _stage;
        private static Stage _previousStage;
        private static UnityEngine.Object _previousSelection;
        private static CharacterModelPreviewSession _model;
        private static AnimationDesignerAdapter _adapter;
        private static bool _ending;
        private static bool _transitioning;
        private static SceneView _sceneView;
        private static Vector3 _viewPivot;
        private static Quaternion _viewRotation;
        private static float _viewSize;
        private static bool _viewOrthographic;
        private static bool _viewLighting;
        private static bool _viewSkybox;
        internal static string Status { get; private set; } = "选择角色、装备和片段后开始编辑。";
        internal static string LastFailureDetail { get; private set; } = string.Empty;
        internal static bool Active => _model?.Instance != null;
        internal static CharacterModelPreviewSession Model => _model;
        internal static AnimationClip LatestExportFor(AnimationClip target) => Active && _adapter != null && _adapter.Clip == target ? _adapter.LatestExport : null;
        internal static string CaptureSetupForClip(AnimationClip target) => Active && _adapter != null && _adapter.Clip == target ? _adapter.CaptureSetup() : null;
        internal static event Action Changed;

        internal static Action<bool> SuspendForClipChange(AnimationClip target, AnimationClip replacementSource = null)
        {
            if (!Active || _adapter == null || (_adapter.Clip != target && _adapter.Clip != replacementSource)) return null;
            string path = AssetDatabase.GetAssetPath(target);
            string originalPath = AssetDatabase.GetAssetPath(_adapter.Clip);
            AnimationDesignerAdapter adapter = _adapter;
            Animator animator = _model.Animator;
            adapter.SetInteractionEnabled(false);
            adapter.Detach();
            return reset =>
            {
                try
                {
                    if (_adapter != adapter || _model?.Animator != animator) return;
                    if (reset) adapter.ReleaseTemporarySetup(animator);
                    adapter.Attach(animator, AssetDatabase.LoadAssetAtPath<AnimationClip>(reset ? path : originalPath));
                }
                finally { adapter.SetInteractionEnabled(true); }
            };
        }

        static AnimationDesignerSession()
        {
            AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
            CompilationPipeline.compilationStarted += _ => End(false);
            EditorApplication.quitting += BeforeReload;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update += Poll;
        }

        internal static bool Apply(CharacterModelPreviewChoice character, string loadout, bool hideWeapons, AnimationClip clip)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return Fail("请退出 Play Mode 后编辑动画。");
            if (character == null || clip == null) return Fail("请选择有效角色和动画片段。");
            if (_clipPreviewMode && !End(false)) return false;
            if (_stage == null && StageUtility.GetCurrentStage() != StageUtility.GetMainStage())
                return Fail("请先退出当前 Prefab／隔离编辑，再打开 Animation Designer 临时舞台。");
            if (_adapter == null && !AnimationDesignerAdapter.TryCreate(out _adapter, out string adapterError)) return Fail(adapterError);
            if (Active && _model.CharacterKey == character.Key)
            {
                if (!AnimationDesignerAdapter.ValidateClip(_model.Animator, clip, out string clipError)) return Fail(clipError);
                if (!_model.TrySetEquipment(loadout, hideWeapons, out string equipmentError)) return Fail(equipmentError);
                _adapter.Attach(_model.Animator, clip);
                Status = "编辑中 · " + character.Label + " · " + loadout + " · " + clip.name;
                Changed?.Invoke();
                return true;
            }

            CharacterModelPreviewSession candidate = null;
            AnimationDesignerAdapter adapter = _adapter;
            Scene staging = default;
            _transitioning = true;
            try
            {
                staging = EditorSceneManager.NewPreviewScene();
                if (!CharacterModelPreviewSession.TryCreate(character, loadout, hideWeapons, staging, out candidate, out string error)) return Fail(error);
                if (!AnimationDesignerAdapter.ValidateClip(candidate.Animator, clip, out error)) return Fail(error);
                if (!_adapter.ConfirmRelease()) return false;
                OpenStageIfNeeded();
                SceneManager.MoveGameObjectToScene(candidate.Instance, _stage.scene);
                var previous = _model;
                // Keep the old hierarchy alive until the plugin has accepted the replacement.
                AnimationClip oldClip = _adapter.Clip;
                try { _adapter.Attach(candidate.Animator, clip); }
                catch
                {
                    if (previous?.Animator != null) _adapter.Attach(previous.Animator, oldClip);
                    throw;
                }
                _model = candidate;
                candidate = null;
                ActiveEditorTracker.sharedTracker.ForceRebuild();
                if (previous != null) _adapter.ReleaseTemporarySetup(previous.Animator);
                previous?.Dispose();
                FocusModel();
                Status = "编辑中 · " + character.Label + " · " + loadout + " · " + clip.name;
                Changed?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                LastFailureDetail = ex.GetBaseException().ToString();
                if (!Active) End(false);
                return Fail(ex.GetBaseException().Message);
            }
            finally
            {
                if (candidate != null) adapter.ReleaseTemporarySetup(candidate.Animator);
                candidate?.Dispose();
                if (staging.IsValid()) EditorSceneManager.ClosePreviewScene(staging);
                _transitioning = false;
            }
        }

        internal static void FocusModel()
        {
            if (!Active) return;
            Selection.activeGameObject = _model.Animator.gameObject;
            SceneView view = SceneView.lastActiveSceneView ?? EditorWindow.GetWindow<SceneView>();
            var renderers = _model.Instance.GetComponentsInChildren<Renderer>();
            Bounds bounds = new Bounds(_model.Instance.transform.position, Vector3.one);
            if (renderers.Length > 0)
            {
                bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            }
            view.Frame(bounds, false);
            view.Repaint();
        }

        internal static bool End(bool askToSave)
        {
            if (_ending) return true;
            if (askToSave && Active && !_clipPreviewMode && !_adapter.ConfirmRelease()) return false;
            _ending = true;
            try
            {
                var stage = _stage;
                bool returnToPrevious = stage != null && StageUtility.GetCurrentStage() == stage;
                if (_model?.Instance != null && Selection.activeGameObject != null
                    && Selection.activeGameObject.transform.IsChildOf(_model.Instance.transform))
                {
                    Selection.activeObject = _previousSelection;
                }
                // The Inspector may still cache the last preview selection even after Selection has changed.
                ActiveEditorTracker.sharedTracker.ForceRebuild();
                StopClipPreview();
                _adapter?.Detach();
                if (_model != null) _adapter?.ReleaseTemporarySetup(_model.Animator);
                _model?.Dispose();
                _model = null;
                _adapter = null;
                _stage = null;
                if (returnToPrevious)
                {
                    if (_previousStage != null) StageUtility.GoToStage(_previousStage, false);
                    else StageUtility.GoToMainStage();
                    Selection.activeObject = _previousSelection;
                }
                if (stage != null) UnityEngine.Object.DestroyImmediate(stage);
                if (_sceneView != null)
                {
                    _sceneView.sceneLighting = _viewLighting;
                    _sceneView.sceneViewState.showSkybox = _viewSkybox;
                    if (returnToPrevious) _sceneView.LookAt(_viewPivot, _viewRotation, _viewSize, _viewOrthographic, true);
                    _sceneView.Repaint(); _sceneView = null;
                }
                _previousStage = null; _previousSelection = null;
                RestoreClipPreviewWindow();
                Status = "临时编辑已结束；选择已保留。";
                Changed?.Invoke();
                return true;
            }
            finally { _ending = false; }
        }

        internal static void StageClosed(AnimationDesignerStage stage)
        {
            if (_ending || _stage != stage) return;
            _stage = null;
            End(false);
        }

        private static void Poll()
        {
            if (_transitioning || _ending) return;
            if (_clipPreviewMode && !Active) { End(false); return; }
            if (!Active) return;
            if (_clipPreviewMode) { PollClipPreview(); return; }
            if (!_adapter.IsOpen || _stage == null || StageUtility.GetCurrentStage() != _stage
                || (_adapter.BoundAnimator != null && _adapter.BoundAnimator != _model.Animator)) End(false);
        }
        private static void OpenStageIfNeeded()
        {
            if (_stage != null) return;
            _previousStage = StageUtility.GetCurrentStage();
            _previousSelection = Selection.activeObject;
            _sceneView = SceneView.lastActiveSceneView ?? EditorWindow.GetWindow<SceneView>();
            _viewPivot = _sceneView.pivot; _viewRotation = _sceneView.rotation; _viewSize = _sceneView.size;
            _viewOrthographic = _sceneView.orthographic; _viewLighting = _sceneView.sceneLighting;
            _viewSkybox = _sceneView.sceneViewState.showSkybox;
            _stage = AnimationDesignerStage.Create();
            StageUtility.GoToStage(_stage, false);
            if (!_stage.scene.IsValid()) throw new InvalidOperationException("无法打开 Animation Designer 临时舞台。");
            _sceneView.sceneLighting = false;
            _sceneView.sceneViewState.showSkybox = false;
        }
        private static void BeforeReload() => End(false);
        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode) End(false);
        }
        private static bool Fail(string diagnostic) { Status = diagnostic; Changed?.Invoke(); return false; }
    }
}

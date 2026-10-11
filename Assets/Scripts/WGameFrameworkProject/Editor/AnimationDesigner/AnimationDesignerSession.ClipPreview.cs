using System;
using System.Reflection;
using MxFramework.Editor.CharacterPreview;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MxFramework.Project.Editor.AnimationDesigner
{
    internal static partial class AnimationDesignerSession
    {
        private static bool _clipPreviewMode;
        private static AnimationClip _clipPreviewClip;
        private static AnimationWindow _clipPreviewWindow;
        private static AnimatorController _clipPreviewController;
        private static AnimatorStateMachine _clipPreviewMachine;
        private static AnimatorState _clipPreviewState;
        private static AnimationClip _previousWindowClip;
        private static float _previousWindowTime;
        private static bool _previousWindowPreviewing;
        private static bool _previousWindowPlaying;
        private static Vector3 _clipPreviewOrigin;
        private static Quaternion _clipPreviewRotation;
        private static float _lastClipPreviewTime;
        private static int _clipPreviewReadyChecks;
        private static bool _clipPreviewRefreshPending;
        private static bool _focusRootCurvesPending;
        private static int _rootCurveFocusAttempts;

        internal static bool IsClipPreviewActive => _clipPreviewMode && Active;
        internal static AnimationClip PreviewClip => IsClipPreviewActive ? _clipPreviewClip : null;

        internal static bool BeginRootMotionPreview(CharacterModelPreviewChoice character, string loadout, bool hideWeapons, AnimationClip clip)
        {
            if (!AnimationDesignerRootCurveFocus.HasRootTranslationCurves(clip))
                return Fail("当前片段没有 Root T.x/y/z 曲线，无法定位根运动编辑项。");
            if (!BeginClipPreview(character, loadout, hideWeapons, clip)) return false;
            _focusRootCurvesPending = true;
            _rootCurveFocusAttempts = 0;
            return true;
        }

        internal static bool BeginClipPreview(CharacterModelPreviewChoice character, string loadout, bool hideWeapons, AnimationClip clip)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                return Fail("请在 Editor 就绪且未运行游戏时预览动画。");
            if (character == null || clip == null) return Fail("请选择角色和动画片段。");
            if (Active && !End(!_clipPreviewMode)) return false;
            if (StageUtility.GetCurrentStage() != StageUtility.GetMainStage())
                return Fail("请先退出当前 Prefab／隔离编辑舞台。");

            CharacterModelPreviewSession candidate = null;
            Scene staging = default;
            _transitioning = true;
            try
            {
                staging = EditorSceneManager.NewPreviewScene();
                if (!CharacterModelPreviewSession.TryCreate(character, loadout, hideWeapons, staging, out candidate, out string error))
                    return Fail(error);
                if (!AnimationDesignerAdapter.ValidateClip(candidate.Animator, clip, out error)) return Fail(error);
                OpenStageIfNeeded();
                SceneManager.MoveGameObjectToScene(candidate.Instance, _stage.scene);
                _model = candidate;
                candidate = null;
                _clipPreviewMode = true;

                _clipPreviewController = new AnimatorController { name = "Animation Designer Clip Preview", hideFlags = HideFlags.HideAndDontSave };
                Undo.IncrementCurrentGroup();
                _clipPreviewController.AddLayer("Clip Preview");
                _clipPreviewMachine = _clipPreviewController.layers[0].stateMachine;
                _clipPreviewState = _clipPreviewMachine.AddState("Selected Clip");
                _clipPreviewState.motion = clip;
                _clipPreviewMachine.defaultState = _clipPreviewState;
                // These editor APIs register Undo for their temporary objects. Do not put preview setup in the user's Undo history.
                Undo.ClearUndo(_clipPreviewController);
                Undo.ClearUndo(_clipPreviewMachine);
                Undo.ClearUndo(_clipPreviewState);
                _model.Animator.runtimeAnimatorController = _clipPreviewController;
                _model.Animator.applyRootMotion = true;
                _clipPreviewOrigin = _model.Animator.transform.position;
                _clipPreviewRotation = _model.Animator.transform.rotation;
                _clipPreviewClip = clip;

                var windows = Resources.FindObjectsOfTypeAll<AnimationWindow>();
                AnimationWindow window = windows.Length > 0 ? windows[0] : null;
                foreach (AnimationWindow candidateWindow in windows)
                    if (candidateWindow.animationClip == clip) { window = candidateWindow; break; }
                if (window == null) window = EditorWindow.GetWindow<AnimationWindow>("Animation");
                if (window.recording)
                    throw new InvalidOperationException("请先关闭 Animation 窗口的录制模式，再启动片段预览。");
                _clipPreviewWindow = window;
                _previousWindowClip = _clipPreviewWindow.animationClip;
                _previousWindowTime = _clipPreviewWindow.time;
                _previousWindowPreviewing = _clipPreviewWindow.previewing;
                _previousWindowPlaying = _clipPreviewWindow.playing;
                _clipPreviewWindow.playing = false;
                _clipPreviewWindow.previewing = false;
                FocusModel();
                _clipPreviewWindow.animationClip = clip;
                // Unity 6000.5 keeps an asset-only selection item after assigning animationClip.
                // Synchronize its GameObject selection so native Preview can bind the temporary Animator.
                MethodInfo synchronizeSelection = typeof(AnimationWindow).GetMethod("OnSelectionChangeInternal",
                    BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(bool) }, null);
                if (synchronizeSelection == null)
                    throw new InvalidOperationException("当前 Unity 版本缺少 Animation 窗口选择同步接口，无法启用原生预览。");
                synchronizeSelection.Invoke(_clipPreviewWindow, new object[] { false });
                _clipPreviewWindow.Focus();
                _clipPreviewWindow.Repaint();
                _clipPreviewReadyChecks = 0;
                _lastClipPreviewTime = 0;
                AnimationUtility.onCurveWasModified += OnClipPreviewCurveModified;
                Undo.undoRedoPerformed += OnClipPreviewUndoRedo;
                Status = "正在连接 Animation 窗口的原生预览…";
                Changed?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                LastFailureDetail = ex.GetBaseException().ToString();
                End(false);
                return Fail(ex.GetBaseException().Message);
            }
            finally
            {
                candidate?.Dispose();
                if (staging.IsValid()) EditorSceneManager.ClosePreviewScene(staging);
                _transitioning = false;
            }
        }

        private static void PollClipPreview()
        {
            if (_stage == null || StageUtility.GetCurrentStage() != _stage || _clipPreviewWindow == null
                || _model?.Animator == null || _clipPreviewClip == null)
            {
                End(false);
                return;
            }
            if (!_clipPreviewWindow.canPreview)
            {
                if (++_clipPreviewReadyChecks <= 8) return;
                End(false);
                Fail("Animation 窗口未接受临时角色，原生 Preview 仍不可用。请检查片段和 Animator。");
                return;
            }
            if (_clipPreviewReadyChecks >= 0)
            {
                _clipPreviewReadyChecks = -1;
                _clipPreviewWindow.previewing = true;
                Status = "片段实时预览中 · 使用 Animation 窗口播放或拖动时间轴；曲线修改立即显示。";
                Changed?.Invoke();
            }
            if (_focusRootCurvesPending)
            {
                if (AnimationDesignerRootCurveFocus.TryFocus(_clipPreviewWindow, _clipPreviewClip, out string diagnostic))
                {
                    _focusRootCurvesPending = false;
                    Status = "根运动曲线已定位 · 在 Animation 窗口编辑 Root T.x/y/z 并实时预览。";
                    Changed?.Invoke();
                }
                else if (++_rootCurveFocusAttempts > 12)
                {
                    _focusRootCurvesPending = false;
                    Status = "片段预览已开启，但 Root T 曲线定位失败：" + diagnostic;
                    Changed?.Invoke();
                }
            }
            if (_clipPreviewRefreshPending) FlushClipPreviewRefresh();
            float time = _clipPreviewWindow.time;
            if (_clipPreviewWindow.playing && time + 0.5f / Mathf.Max(1f, _clipPreviewClip.frameRate) < _lastClipPreviewTime)
            {
                _model.Animator.transform.SetPositionAndRotation(_clipPreviewOrigin, _clipPreviewRotation);
                SceneView.RepaintAll();
            }
            _lastClipPreviewTime = time;
        }

        private static void OnClipPreviewCurveModified(AnimationClip clip, EditorCurveBinding binding, AnimationUtility.CurveModifiedType kind)
        {
            if (!_clipPreviewMode || clip != _clipPreviewClip) return;
            RefreshClipPreview();
        }

        private static void OnClipPreviewUndoRedo()
        {
            if (_clipPreviewMode) RefreshClipPreview();
        }

        private static void RefreshClipPreview()
        {
            if (_clipPreviewWindow == null) return;
            _clipPreviewRefreshPending = true;
        }

        private static void FlushClipPreviewRefresh()
        {
            _clipPreviewRefreshPending = false;
            if (!_clipPreviewMode || _clipPreviewWindow == null || _clipPreviewClip == null) return;
            if (_clipPreviewWindow.previewing && !_clipPreviewWindow.playing)
            {
                float time = _clipPreviewWindow.time;
                float step = 1f / Mathf.Max(1f, _clipPreviewClip.frameRate);
                float alternate = time + step <= _clipPreviewClip.length ? time + step : Mathf.Max(0f, time - step);
                if (!Mathf.Approximately(alternate, time))
                {
                    _clipPreviewWindow.time = alternate;
                    _clipPreviewWindow.time = time;
                }
            }
            _clipPreviewWindow.Repaint();
            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
        }

        private static void StopClipPreview()
        {
            if (!_clipPreviewMode) return;
            AnimationUtility.onCurveWasModified -= OnClipPreviewCurveModified;
            Undo.undoRedoPerformed -= OnClipPreviewUndoRedo;
            _clipPreviewRefreshPending = false;
            _focusRootCurvesPending = false;
            _rootCurveFocusAttempts = 0;
            if (_clipPreviewWindow != null)
            {
                _clipPreviewWindow.playing = false;
                _clipPreviewWindow.previewing = false;
            }
            if (_model?.Animator != null) _model.Animator.runtimeAnimatorController = null;
            if (_clipPreviewState != null) Object.DestroyImmediate(_clipPreviewState);
            if (_clipPreviewMachine != null) Object.DestroyImmediate(_clipPreviewMachine);
            if (_clipPreviewController != null) Object.DestroyImmediate(_clipPreviewController);
            _clipPreviewState = null;
            _clipPreviewMachine = null;
            _clipPreviewController = null;
            _clipPreviewClip = null;
            _clipPreviewMode = false;
        }

        private static void RestoreClipPreviewWindow()
        {
            if (_clipPreviewWindow == null)
            {
                _clipPreviewWindow = null;
                _previousWindowClip = null;
                _previousWindowTime = 0;
                _previousWindowPreviewing = false;
                _previousWindowPlaying = false;
                return;
            }
            try
            {
                _clipPreviewWindow.animationClip = _previousWindowClip;
                _clipPreviewWindow.time = _previousWindowTime;
                if (_previousWindowPreviewing && _clipPreviewWindow.canPreview) _clipPreviewWindow.previewing = true;
                if (_previousWindowPlaying && _clipPreviewWindow.canPreview) _clipPreviewWindow.playing = true;
                _clipPreviewWindow.Repaint();
            }
            finally
            {
                _clipPreviewWindow = null;
                _previousWindowClip = null;
                _previousWindowTime = 0;
                _previousWindowPreviewing = false;
                _previousWindowPlaying = false;
            }
        }
    }
}

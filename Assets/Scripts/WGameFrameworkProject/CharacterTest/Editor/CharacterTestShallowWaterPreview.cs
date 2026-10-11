using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace MxFramework.Game.CharacterTest.Editor
{
    /// <summary>Derived Game/Scene preview. Never saves assets or advances gameplay/presentation.</summary>
    [InitializeOnLoad]
    internal static class CharacterTestShallowWaterPreview
    {
        private const double PollInterval = .25;
        private const double AnimationInterval = 1d / 15;
        private static readonly List<Entry> Entries = new List<Entry>();
        private static readonly List<SceneView> VisibleScenes = new List<SceneView>();
        private static readonly BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly FieldInfo ParentField = typeof(EditorWindow).GetField("m_Parent", InstanceFlags);
        private static EditorWindow _gameView;
        private static double _nextPoll, _nextAnimation;
        private static bool _invalidated = true, _framePending, _flushQueued, _rendering, _gameVisible, _quitting, _transitioning;
        private static int _renderContextDepth;

        private sealed class Entry
        {
            internal CharacterTestShallowWaterSession Sample;
            internal RenderTexture Target;
            internal int Signature;
            internal bool Seen, ReflectionPending = true;
            internal readonly List<SceneEntry> Scenes = new List<SceneEntry>();
        }
        private sealed class SceneEntry
        {
            internal SceneView Window;
            internal Camera Camera;
            internal RenderTexture Target;
            internal int Signature;
            internal bool Seen, ReflectionPending = true;
        }

        static CharacterTestShallowWaterPreview()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += PlayModeChanged;
            EditorApplication.quitting += () => { _quitting = true; Clear(); };
            AssemblyReloadEvents.beforeAssemblyReload += Clear;
            EditorSceneManager.sceneOpened += SceneOpened;
            EditorSceneManager.sceneClosed += SceneClosed;
            SceneManager.activeSceneChanged += ActiveSceneChanged;
            Undo.undoRedoPerformed += Invalidate;
            EditorApplication.projectChanged += Invalidate;
            ObjectChangeEvents.changesPublished += ObjectChanges;
            SceneView.duringSceneGui += SceneGui;
            RenderPipelineManager.beginContextRendering += BeginContext;
            RenderPipelineManager.endContextRendering += EndContext;
        }

        internal static void Invalidate() { _invalidated = true; _nextPoll = 0; }
        private static void SceneOpened(Scene scene, OpenSceneMode mode) => Invalidate();
        private static void SceneClosed(Scene scene) { Clear(); Invalidate(); }
        private static void ActiveSceneChanged(Scene previous, Scene current) { Clear(); Invalidate(); }
        private static void PlayModeChanged(PlayModeStateChange state)
        {
            _transitioning = state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.ExitingPlayMode;
            Clear(); Invalidate();
        }
        private static void Clear()
        {
            foreach (var entry in Entries)
                if (entry.Sample != null)
                {
                    entry.Sample.ReleaseScenePreviews();
                    entry.Sample.Owner?.ReleaseShallowWater(entry.Sample);
                }
            Entries.Clear(); VisibleScenes.Clear(); _gameView = null; _gameVisible = false;
            _framePending = _flushQueued = _rendering = false; _renderContextDepth = 0;
        }
        // A GUI event can arrive inside rendering. It may schedule work but must not submit a nested request.
        private static void SceneGui(SceneView scene)
        { if (Event.current != null && Event.current.type != EventType.Repaint) _nextAnimation = 0; }
        private static void BeginContext(ScriptableRenderContext context, List<Camera> cameras) => _renderContextDepth++;
        private static void EndContext(ScriptableRenderContext context, List<Camera> cameras) => _renderContextDepth = Mathf.Max(0, _renderContextDepth - 1);

        private static void ObjectChanges(ref ObjectChangeEventStream changes)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            for (int i = 0; i < changes.length; i++)
            {
                var kind = changes.GetEventType(i);
                Object changed = null;
                if (kind == ObjectChangeKind.ChangeGameObjectOrComponentProperties)
                {
                    changes.GetChangeGameObjectOrComponentPropertiesEvent(i, out var change);
                    changed = EditorUtility.EntityIdToObject(change.entityId);
                }
                else if (kind == ObjectChangeKind.ChangeAssetObjectProperties)
                {
                    changes.GetChangeAssetObjectPropertiesEvent(i, out var change);
                    changed = EditorUtility.EntityIdToObject(change.entityId);
                }
                else if (kind == ObjectChangeKind.None) continue;
                else { Invalidate(); return; }
                if (changed == null) continue;
                var go = changed as GameObject ?? (changed as Component)?.gameObject;
                if ((changed.hideFlags & HideFlags.DontSave) != 0 ||
                    (go != null && (go.hideFlags & HideFlags.DontSave) != 0)) continue;
                Invalidate(); return;
            }
        }

        // LowFreqAlloc: authored sample/window discovery and asset fingerprints run at most four times per second.
        private static void Update()
        {
            if (_quitting || _transitioning || _rendering || _renderContextDepth != 0
                || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (_flushQueued) Flush();
            double now = EditorApplication.timeSinceStartup;
            if (now >= _nextPoll) { _nextPoll = now + PollInterval; Poll(); }
            if (!_gameVisible && VisibleScenes.Count == 0) return;
            if (now >= _nextAnimation)
            {
                _nextAnimation = now + AnimationInterval;
                foreach (var entry in Entries)
                {
                    RefreshScenes(entry, false);
                    var sample = entry.Sample;
                    if (sample != null && sample.IsActive && sample.SurfaceEnabled && sample.Settings != null
                        && (sample.Settings.BackgroundSlope > 0 || sample.ActiveRipples > 0 || EditorApplication.isPlaying))
                        _framePending = true;
                }
            }
            if (_framePending && !_flushQueued) _flushQueued = true;
        }

        private static bool Visible(EditorWindow window)
        {
            var parent = ParentField?.GetValue(window);
            var actual = parent?.GetType().GetProperty("actualView", InstanceFlags);
            return actual == null || ReferenceEquals(actual.GetValue(parent, null), window);
        }

        private static void Poll()
        {
            bool wasVisible = _gameVisible;
            _gameView = null; VisibleScenes.Clear();
            foreach (var window in UnityEngine.Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                if (!Visible(window)) continue;
                if (window.GetType().FullName == "UnityEditor.GameView") _gameView = window;
                else if (window is SceneView sceneView && sceneView.camera != null) VisibleScenes.Add(sceneView);
            }
            _gameVisible = _gameView != null;
            if (_gameVisible && !wasVisible) _invalidated = true;
            foreach (var entry in Entries) entry.Seen = false;
            var scene = SceneManager.GetActiveScene();
            if (scene.isLoaded)
            {
                foreach (var root in scene.GetRootGameObjects())
                foreach (var owner in root.GetComponentsInChildren<GameManager>(true))
                {
                    var sample = owner.SynchronizeShallowWater();
                    if (sample == null || !sample.IsActive) continue;
                    Entry entry = null;
                    foreach (var candidate in Entries) if (ReferenceEquals(candidate.Sample, sample)) { entry = candidate; break; }
                    if (entry == null) { entry = new Entry { Sample = sample }; Entries.Add(entry); _framePending = true; }
                    entry.Seen = true;
                    sample.PrepareView();
                    int signature = Signature(sample);
                    var target = sample.ReflectionTexture;
                    bool changed = _invalidated || entry.Signature != signature;
                    if (changed || !ReferenceEquals(entry.Target, target) || (target != null && !sample.ReflectionReady))
                    { entry.ReflectionPending = true; _framePending = true; }
                    entry.Signature = signature; entry.Target = target;
                    SynchronizeScenes(entry);
                    RefreshScenes(entry, changed);
                }
            }
            for (int i = Entries.Count - 1; i >= 0; i--)
                if (!Entries[i].Seen)
                { if (Entries[i].Sample != null) { Entries[i].Sample.ReleaseScenePreviews(); Entries[i].Sample.Owner?.ReleaseShallowWater(Entries[i].Sample); } Entries.RemoveAt(i); _framePending = true; }
            _invalidated = false;
        }

        private static void SynchronizeScenes(Entry entry)
        {
            foreach (var scene in entry.Scenes) scene.Seen = false;
            // Do not draw the main scene's water into an isolated Prefab Stage.
            if (StageUtility.GetCurrentStageHandle().Contains(entry.Sample.Owner.gameObject))
            foreach (var window in VisibleScenes)
            {
                SceneEntry scene = null;
                foreach (var candidate in entry.Scenes) if (ReferenceEquals(candidate.Window, window)) { scene = candidate; break; }
                if (scene == null)
                { scene = new SceneEntry { Window = window, Camera = window.camera }; entry.Scenes.Add(scene); _framePending = true; }
                if (!ReferenceEquals(scene.Camera, window.camera))
                { entry.Sample.ReleaseScenePreview(scene.Camera); scene.Camera = window.camera; scene.Target = null; scene.ReflectionPending = true; }
                scene.Seen = true;
            }
            for (int i = entry.Scenes.Count - 1; i >= 0; i--)
                if (!entry.Scenes[i].Seen)
                { entry.Sample.ReleaseScenePreview(entry.Scenes[i].Camera); entry.Scenes.RemoveAt(i); _framePending = true; }
        }

        private static void RefreshScenes(Entry entry, bool invalidated)
        {
            if (entry.Sample == null) return;
            foreach (var scene in entry.Scenes)
            {
                if (scene.Window == null || scene.Camera == null || !entry.Sample.PrepareScenePreview(scene.Camera, out var view)) continue;
                int signature = CameraSignature(scene.Camera);
                if (invalidated || signature != scene.Signature || !ReferenceEquals(scene.Target, view.Texture)
                    || (view.Wanted && !view.Ready))
                { scene.ReflectionPending = true; _framePending = true; }
                scene.Signature = signature; scene.Target = view.Texture;
            }
        }

        private static int CameraSignature(Camera camera)
        {
            if (camera == null) return 0;
            unchecked
            {
                int hash = camera.worldToCameraMatrix.GetHashCode();
                hash = hash * 31 + camera.projectionMatrix.GetHashCode();
                hash = hash * 31 + camera.pixelWidth; hash = hash * 31 + camera.pixelHeight;
                hash = hash * 31 + camera.cullingMask;
                return hash * 31 + (camera.isActiveAndEnabled ? 1 : 0);
            }
        }

        private static int Signature(CharacterTestShallowWaterSession sample)
        {
            unchecked
            {
                int hash = sample.LocalToWorld.GetHashCode();
                hash = hash * 31 + (sample.Settings != null ? JsonUtility.ToJson(sample.Settings).GetHashCode() : 0);
                hash = hash * 31 + (sample.SurfaceEnabled ? 1 : 0);
                hash = hash * 31 + (sample.ReflectionEnabled ? 1 : 0);
                hash = hash * 31 + (sample.RipplesEnabled ? 1 : 0);
                var configuration = sample.Owner?.SceneRendering;
                hash = hash * 31 + (configuration != null ? JsonUtility.ToJson(configuration).GetHashCode() : 0);
                return hash * 31 + CameraSignature(configuration?.TargetCamera);
            }
        }

        private static void Flush()
        {
            _flushQueued = false;
            if (_quitting || _transitioning || _rendering || _renderContextDepth != 0 ||
                EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            _rendering = true; _framePending = false;
            try
            {
                foreach (var entry in Entries)
                {
                    var sample = entry.Sample;
                    if (sample == null || !sample.IsActive) continue;
                    if (!EditorApplication.isPlaying && _gameVisible && entry.ReflectionPending)
                    { entry.ReflectionPending = false; sample.RenderReflectionNow(); }
                    foreach (var scene in entry.Scenes)
                    {
                        if (scene.Window == null) continue;
                        if (scene.ReflectionPending)
                        {
                            scene.ReflectionPending = false;
                            sample.RenderScenePreviewReflection(scene.Camera);
                        }
                        scene.Window.Repaint();
                    }
                }
                if (!EditorApplication.isPlaying)
                {
                    EditorApplication.QueuePlayerLoopUpdate();
                    if (_gameVisible && _gameView != null) _gameView.Repaint();
                }
            }
            finally { _rendering = false; _renderContextDepth = 0; }
        }
    }
}

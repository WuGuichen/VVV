using System;
using MxFramework.Core.Math;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace MxFramework.Editor.CharacterAction.Timeline.Preview
{
    /// <summary>
    /// UI Toolkit host for the isolated, read-only Character Action preview surface.
    /// </summary>
    /// <remarks>
    /// Allocation category: AllocByDesign during construction, source changes, and AutoFrame.
    /// Repaint and overlay drawing reuse initialized controls, GUI content, and preview scene state.
    /// </remarks>
    internal sealed class CharacterActionPreviewViewport : VisualElement, IDisposable
    {
        private const string PreferencePrefix = "MxFramework.CharacterAction.Preview.";
        private const string EmptyPreviewMessage = "选择一个动作以开始预览。";
        private const string SceneViewMessage = "场景编辑模式已启用；内嵌预览已释放。";
        private static readonly Color GridColor = new Color(0.16f, 0.19f, 0.23f, 0.85f);
        private static readonly Color HitVolumeColor = new Color(0.25f, 0.95f, 1f, 0.95f);
        private static readonly Color TraceColor = new Color(1f, 0.55f, 0.05f, 0.95f);
        private static readonly Color LocatorColor = new Color(0.2f, 1f, 0.45f, 0.95f);
        private static readonly Color VfxColor = new Color(0.9f, 0.35f, 1f, 0.95f);
        private static readonly Color ProjectileColor = new Color(1f, 0.82f, 0.2f, 0.95f);
        private static readonly Color RootMotionColor = new Color(0.3f, 0.75f, 1f, 0.95f);

        private readonly IMGUIContainer _canvas;
        private readonly CharacterActionPreviewRenderScene _renderScene;
        private readonly Action<UnityEngine.Camera> _drawOverlays;
        private readonly GUIContent _diagnosticContent = new GUIContent();
        private CharacterActionPreviewRuntimeSession _session;
        private CharacterActionPreviewSurfaceMode _surfaceMode;
        private GameObject _lastAutoFramedInstance;
        private bool _showGrid = true;
        private bool _showTrajectories = true;
        private bool _showHitVolumes = true;
        private bool _showRootMotion = true;
        private bool _showLocators = true;
        private bool _showVfx = true;
        private bool _showProjectiles = true;
        private bool _disposed;

        public CharacterActionPreviewViewport()
        {
            name = "character-action-preview-viewport";
            AddToClassList("ca-preview-viewport");
            style.flexGrow = 1f;
            style.minHeight = 160f;

            _drawOverlays = DrawReadOnlyOverlays;
            _showGrid = EditorPrefs.GetBool(PreferencePrefix + "Grid", true);
            _showTrajectories = EditorPrefs.GetBool(PreferencePrefix + "Trajectories", true);
            _showHitVolumes = EditorPrefs.GetBool(PreferencePrefix + "HitVolumes", true);
            _showRootMotion = EditorPrefs.GetBool(PreferencePrefix + "RootMotion", false);
            _showLocators = EditorPrefs.GetBool(PreferencePrefix + "Locators", true);
            _showVfx = EditorPrefs.GetBool(PreferencePrefix + "Vfx", true);
            _showProjectiles = EditorPrefs.GetBool(PreferencePrefix + "Projectiles", true);
            _renderScene = new CharacterActionPreviewRenderScene(RequestRepaint);
            _session = new CharacterActionPreviewRuntimeSession(_renderScene);
            _surfaceMode = CharacterActionPreviewSurfaceMode.Embedded;
            _diagnosticContent.text = EmptyPreviewMessage;

            Add(BuildToolbar());
            _canvas = new IMGUIContainer(DrawCanvas)
            {
                name = "character-action-preview-canvas",
                focusable = true,
            };
            _canvas.AddToClassList("ca-preview-canvas");
            _canvas.style.flexGrow = 1f;
            _canvas.style.minHeight = 120f;
            Add(_canvas);
        }

        public CharacterActionPreviewSurfaceMode SurfaceMode => _surfaceMode;
        public CharacterActionPreviewRenderScene RenderScene => _renderScene;
        public CharacterActionPreviewRuntimeSession Session => _session;
        public string DiagnosticsText => _diagnosticContent.text ?? string.Empty;
        public bool IsDisposed => _disposed;

        public bool ShowGrid
        {
            get => _showGrid;
            set => SetOverlayFlag(ref _showGrid, value, "Grid");
        }

        public bool ShowTrajectories
        {
            get => _showTrajectories;
            set => SetOverlayFlag(ref _showTrajectories, value, "Trajectories");
        }

        public bool ShowHitVolumes
        {
            get => _showHitVolumes;
            set => SetOverlayFlag(ref _showHitVolumes, value, "HitVolumes");
        }

        public bool ShowRootMotion
        {
            get => _showRootMotion;
            set => SetOverlayFlag(ref _showRootMotion, value, "RootMotion");
        }

        public bool ShowLocators
        {
            get => _showLocators;
            set => SetOverlayFlag(ref _showLocators, value, "Locators");
        }

        public bool ShowVfx
        {
            get => _showVfx;
            set => SetOverlayFlag(ref _showVfx, value, "Vfx");
        }

        public bool ShowProjectiles
        {
            get => _showProjectiles;
            set => SetOverlayFlag(ref _showProjectiles, value, "Projectiles");
        }

        public void PreviewFrame(CharacterActionAuthoringModel model, int targetFrame, long revisionToken)
        {
            PreviewFrame(model, targetFrame, revisionToken, externalContext: null);
        }

        internal void PreviewFrame(
            CharacterActionAuthoringModel model,
            int targetFrame,
            long revisionToken,
            CharacterActionAuthoringExportResult externalContext)
        {
            if (_disposed || _surfaceMode != CharacterActionPreviewSurfaceMode.Embedded || _session == null)
                return;

            _session.PreviewFrame(model, targetFrame, revisionToken, externalContext);
            SetDiagnostics(_session.DiagnosticsText);
            GameObject preview = _session.PreviewInstance;
            if (preview != null && preview != _lastAutoFramedInstance)
            {
                _lastAutoFramedInstance = preview;
                _renderScene.AutoFrame(preview);
            }

            RequestRepaint();
        }

        public void ClearPreview()
        {
            if (_disposed)
                return;

            _session?.RegeneratePreview(refreshAssetDatabase: false);
            _lastAutoFramedInstance = null;
            SetDiagnostics(_surfaceMode == CharacterActionPreviewSurfaceMode.Embedded
                ? EmptyPreviewMessage
                : SceneViewMessage);
        }

        public void SetSurfaceMode(CharacterActionPreviewSurfaceMode mode)
        {
            if (_disposed || _surfaceMode == mode)
                return;

            _session?.Dispose();
            _session = null;
            _lastAutoFramedInstance = null;
            _surfaceMode = mode;
            if (mode == CharacterActionPreviewSurfaceMode.Embedded)
            {
                _session = new CharacterActionPreviewRuntimeSession(_renderScene);
                SetDiagnostics(EmptyPreviewMessage);
            }
            else
            {
                SetDiagnostics(SceneViewMessage);
            }

            RequestRepaint();
        }

        public void ResetCamera()
        {
            if (!_disposed)
                _renderScene.ResetCamera();
        }

        public bool AutoFrame()
        {
            return !_disposed && _renderScene.AutoFrame(_session?.PreviewInstance);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _session?.Dispose();
            _session = null;
            _renderScene.Dispose();
            _lastAutoFramedInstance = null;
            _canvas.onGUIHandler = null;
        }

        private VisualElement BuildToolbar()
        {
            var toolbar = new VisualElement { name = "character-action-preview-toolbar" };
            toolbar.AddToClassList("ca-preview-toolbar");
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.flexWrap = Wrap.Wrap;
            toolbar.style.flexShrink = 0f;

            toolbar.Add(CreateToggle("网格", _showGrid, value => ShowGrid = value));
            toolbar.Add(CreateToggle("轨迹", _showTrajectories, value => ShowTrajectories = value));
            toolbar.Add(CreateToggle("碰撞体", _showHitVolumes, value => ShowHitVolumes = value));
            toolbar.Add(CreateToggle("根运动", _showRootMotion, value => ShowRootMotion = value));
            toolbar.Add(CreateToggle("挂点", _showLocators, value => ShowLocators = value));
            toolbar.Add(CreateToggle("特效", _showVfx, value => ShowVfx = value));
            toolbar.Add(CreateToggle("投射物", _showProjectiles, value => ShowProjectiles = value));

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1f;
            toolbar.Add(spacer);

            toolbar.Add(new Button(ResetCamera) { text = "重置视角", name = "preview-reset-camera" });
            toolbar.Add(new Button(() => AutoFrame()) { text = "自动取景", name = "preview-auto-frame" });
            return toolbar;
        }

        private static Toggle CreateToggle(string text, bool initialValue, Action<bool> changed)
        {
            var toggle = new Toggle(text) { value = initialValue };
            toggle.RegisterValueChangedCallback(evt => changed(evt.newValue));
            return toggle;
        }

        private void DrawCanvas()
        {
            if (_disposed)
                return;

            Rect rect = new Rect(0f, 0f, _canvas.contentRect.width, _canvas.contentRect.height);
            HandleCameraInput(rect);
            if (_surfaceMode == CharacterActionPreviewSurfaceMode.Embedded)
                _renderScene.Draw(rect, _drawOverlays);
            else if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(rect, new Color(0.025f, 0.03f, 0.04f, 1f));

            if (Event.current.type == EventType.Repaint
                && (_session?.PreviewInstance == null || !string.IsNullOrWhiteSpace(DiagnosticsText)))
            {
                DrawDiagnostics(rect);
            }
        }

        private void HandleCameraInput(Rect rect)
        {
            if (_surfaceMode != CharacterActionPreviewSurfaceMode.Embedded)
                return;

            Event current = Event.current;
            if (!rect.Contains(current.mousePosition))
                return;

            if (current.type == EventType.MouseDrag)
            {
                if (current.button == 2 || (current.button == 0 && current.shift))
                {
                    _renderScene.Pan(current.delta);
                    current.Use();
                }
                else if (current.button == 0)
                {
                    _renderScene.Orbit(current.delta);
                    current.Use();
                }
            }
            else if (current.type == EventType.ScrollWheel)
            {
                _renderScene.Zoom(current.delta.y);
                current.Use();
            }
        }

        private void DrawReadOnlyOverlays(UnityEngine.Camera camera)
        {
            if (camera == null || Event.current.type != EventType.Repaint)
                return;

            Handles.SetCamera(camera);
            CompareFunction previousZTest = Handles.zTest;
            Color previousColor = Handles.color;
            Handles.zTest = CompareFunction.LessEqual;

            if (_showGrid)
                DrawGrid();
            CharacterActionPreviewRuntimeSession session = _session;
            if (session != null)
            {
                if (_showRootMotion)
                    DrawRootMotion(session);
                if (_showLocators)
                    DrawLocators(session);
                if (_showHitVolumes)
                    DrawHitVolumes(session);
                if (_showTrajectories)
                    DrawWeaponTraces(session);
                if (_showVfx)
                    DrawVfx(session);
                if (_showProjectiles || _showTrajectories)
                    DrawProjectiles(session, _showProjectiles, _showTrajectories);
            }

            Handles.color = previousColor;
            Handles.zTest = previousZTest;
        }

        private static void DrawGrid()
        {
            Handles.color = GridColor;
            for (int i = -8; i <= 8; i++)
            {
                Handles.DrawLine(new Vector3(i, 0f, -8f), new Vector3(i, 0f, 8f));
                Handles.DrawLine(new Vector3(-8f, 0f, i), new Vector3(8f, 0f, i));
            }
        }

        private static void DrawRootMotion(CharacterActionPreviewRuntimeSession session)
        {
            GameObject preview = session.PreviewInstance;
            if (preview == null)
                return;

            Vector3 position = preview.transform.position;
            Handles.color = RootMotionColor;
            Handles.DrawLine(Vector3.zero, position);
            Handles.SphereHandleCap(0, position, Quaternion.identity, 0.08f, EventType.Repaint);
        }

        private static void DrawHitVolumes(CharacterActionPreviewRuntimeSession session)
        {
            var snapshots = session.HitVolumeSnapshots;
            Handles.color = HitVolumeColor;
            for (int i = 0; i < snapshots.Count; i++)
            {
                CharacterActionPreviewRuntimeSession.CharacterActionPreviewHitVolumeSnapshot snapshot = snapshots[i];
                DrawCapsule(snapshot.PointA, snapshot.PointB, Mathf.Max(0.001f, snapshot.Radius));
            }
        }

        private static void DrawWeaponTraces(CharacterActionPreviewRuntimeSession session)
        {
            var snapshots = session.WeaponTraceSnapshots;
            Handles.color = TraceColor;
            for (int i = 0; i < snapshots.Count; i++)
            {
                var trace = snapshots[i].TraceFrame;
                DrawCapsule(ToVector3(trace.RootNow), ToVector3(trace.TipNow), Mathf.Max(0.001f, ToFloat(trace.Radius)));
            }
        }

        private static void DrawLocators(CharacterActionPreviewRuntimeSession session)
        {
            var snapshots = session.WeaponLocatorPoseSnapshots;
            Handles.color = LocatorColor;
            for (int i = 0; i < snapshots.Count; i++)
            {
                CharacterActionPreviewRuntimeSession.CharacterActionPreviewWeaponLocatorPoseSnapshot snapshot = snapshots[i];
                if (!session.TryResolveWeaponLocatorPose(snapshot, out var pose))
                    continue;

                float radius = Mathf.Max(0.015f, snapshot.Radius);
                Vector3 axis = pose.Tip - pose.Root;
                Vector3 normal = axis.sqrMagnitude > 0.000001f ? axis.normalized : Vector3.up;
                Handles.DrawLine(pose.Root, pose.Tip);
                Handles.DrawWireDisc(pose.Root, normal, radius);
                Handles.DrawWireDisc(pose.Tip, normal, radius);
            }
        }

        private static void DrawVfx(CharacterActionPreviewRuntimeSession session)
        {
            var snapshots = session.VfxPreviewSnapshots;
            Handles.color = VfxColor;
            for (int i = 0; i < snapshots.Count; i++)
            {
                CharacterActionPreviewRuntimeSession.CharacterActionPreviewVfxSnapshot snapshot = snapshots[i];
                Handles.DrawWireDisc(snapshot.Position, Vector3.up, 0.2f);
                Handles.DrawLine(snapshot.Position, snapshot.Position + snapshot.Forward * 0.45f);
            }
        }

        private static void DrawProjectiles(
            CharacterActionPreviewRuntimeSession session,
            bool showBodies,
            bool showTrajectories)
        {
            var snapshots = session.ProjectilePreviewSnapshots;
            Handles.color = ProjectileColor;
            for (int i = 0; i < snapshots.Count; i++)
            {
                CharacterActionPreviewRuntimeSession.CharacterActionPreviewProjectileSnapshot snapshot = snapshots[i];
                if (showTrajectories)
                {
                    Handles.DrawLine(snapshot.Origin, snapshot.Position);
                    Handles.DrawLine(snapshot.Position, snapshot.TargetPoint);
                }

                if (showBodies)
                {
                    float radius = Mathf.Max(0.04f, snapshot.Radius);
                    Handles.SphereHandleCap(0, snapshot.Position, Quaternion.identity, radius * 2f, EventType.Repaint);
                    if (snapshot.IsAreaPolicy && snapshot.AreaRadius > 0f)
                        Handles.DrawWireDisc(snapshot.Position, Vector3.up, snapshot.AreaRadius);
                }
            }
        }

        private static void DrawCapsule(Vector3 pointA, Vector3 pointB, float radius)
        {
            Vector3 axis = pointB - pointA;
            Vector3 normal = axis.sqrMagnitude > 0.000001f ? axis.normalized : Vector3.up;
            Vector3 tangent = Vector3.Cross(normal, Vector3.up);
            if (tangent.sqrMagnitude < 0.000001f)
                tangent = Vector3.Cross(normal, Vector3.right);
            tangent.Normalize();
            Vector3 bitangent = Vector3.Cross(normal, tangent).normalized;

            Handles.DrawLine(pointA, pointB);
            Handles.DrawWireDisc(pointA, normal, radius);
            Handles.DrawWireDisc(pointB, normal, radius);
            Handles.DrawLine(pointA + tangent * radius, pointB + tangent * radius);
            Handles.DrawLine(pointA - tangent * radius, pointB - tangent * radius);
            Handles.DrawLine(pointA + bitangent * radius, pointB + bitangent * radius);
            Handles.DrawLine(pointA - bitangent * radius, pointB - bitangent * radius);
        }

        private void DrawDiagnostics(Rect rect)
        {
            GUIStyle style = EditorStyles.wordWrappedMiniLabel;
            Rect labelRect = new Rect(rect.x + 18f, rect.yMax - 62f, Mathf.Max(0f, rect.width - 36f), 44f);
            GUI.Box(new Rect(labelRect.x - 8f, labelRect.y - 6f, labelRect.width + 16f, labelRect.height + 12f), GUIContent.none);
            GUI.Label(labelRect, _diagnosticContent, style);
        }

        private void SetDiagnostics(string text)
        {
            string normalized = string.IsNullOrWhiteSpace(text) ? string.Empty : text;
            if (string.Equals(_diagnosticContent.text, normalized, StringComparison.Ordinal))
                return;

            _diagnosticContent.text = normalized;
            RequestRepaint();
        }

        private void SetOverlayFlag(ref bool field, bool value, string preferenceSuffix)
        {
            if (field == value)
                return;

            field = value;
            EditorPrefs.SetBool(PreferencePrefix + preferenceSuffix, value);
            RequestRepaint();
        }

        private void RequestRepaint()
        {
            if (!_disposed)
                _canvas?.MarkDirtyRepaint();
        }

        private static Vector3 ToVector3(FixVector3 value)
        {
            return new Vector3(ToFloat(value.X), ToFloat(value.Y), ToFloat(value.Z));
        }

        private static float ToFloat(Fix64 value)
        {
            return value.RawValue / (float)Fix64.Scale;
        }
    }
}

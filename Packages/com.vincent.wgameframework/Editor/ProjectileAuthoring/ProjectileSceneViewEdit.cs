using System;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor.ProjectileAuthoring
{
    /// <summary>Projectile-specific SceneView handles that commit only typed authoring mutations.</summary>
    /// <remarks>Drag repaint is transient; one typed mutation is emitted on mouse-up. No Scene objects are created or modified.</remarks>
    public sealed class ProjectileSceneViewEdit : IDisposable
    {
        private readonly Action<ProjectileAuthoringFieldEditView> _commit;
        private readonly Func<ProjectileDefinitionListItem> _definition;
        private readonly Func<ProjectileRequestVariantListItem> _request;
        private readonly Func<Vector3> _previewTarget;
        private readonly Action<Vector3, bool> _setPreviewTarget;
        private readonly Func<string> _previewTargetLabel;
        private ProjectileAuthoringFieldEditView _pending;
        private bool _registered;
        private bool _previewTargetDragging;
        private Vector3 _previewTargetBeforeDrag;

        public ProjectileSceneViewEdit(Action<ProjectileAuthoringFieldEditView> commit, Func<ProjectileDefinitionListItem> definition,
            Func<ProjectileRequestVariantListItem> request, Func<Vector3> previewTarget = null, Action<Vector3, bool> setPreviewTarget = null,
            Func<string> previewTargetLabel = null)
        { _commit = commit ?? throw new ArgumentNullException(nameof(commit)); _definition = definition ?? throw new ArgumentNullException(nameof(definition)); _request = request ?? throw new ArgumentNullException(nameof(request)); _previewTarget = previewTarget; _setPreviewTarget = setPreviewTarget; _previewTargetLabel = previewTargetLabel; }

        public bool IsRegistered => _registered;
        public bool HasPendingDrag => _pending != null;

        public void Begin()
        {
            if (_registered) return;
            _registered = true;
            SceneView.duringSceneGui += OnSceneGui;
            SceneView.RepaintAll();
        }

        public void End(bool cancelPending)
        {
            if (cancelPending) _pending = null;
            if (cancelPending && _previewTargetDragging)
            {
                _setPreviewTarget?.Invoke(_previewTargetBeforeDrag, false);
                _previewTargetDragging = false;
            }
            if (!_registered) return;
            _registered = false;
            SceneView.duringSceneGui -= OnSceneGui;
            SceneView.RepaintAll();
        }

        public void Dispose() => End(cancelPending: true);

        internal void CommitPendingForTests()
        {
            if (_pending == null) return;
            ProjectileAuthoringFieldEditView value = _pending; _pending = null; _commit(value);
        }

        internal void CancelPendingForTests() => _pending = null;
        internal void StageForTests(ProjectileAuthoringFieldEditView edit) => _pending = edit;
        internal void StagePreviewTargetForTests(Vector3 value)
        {
            if (_previewTarget == null || _setPreviewTarget == null) return;
            if (!_previewTargetDragging) _previewTargetBeforeDrag = _previewTarget();
            _previewTargetDragging = true;
            _setPreviewTarget(value, false);
        }
        internal void CommitPreviewTargetForTests()
        {
            if (!_previewTargetDragging) return;
            _previewTargetDragging = false;
            _setPreviewTarget?.Invoke(_previewTarget(), true);
        }

        private void OnSceneGui(SceneView sceneView)
        {
            Event evt = Event.current;
            if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape)
            {
                _pending = null;
                if (_previewTargetDragging) _setPreviewTarget?.Invoke(_previewTargetBeforeDrag, false);
                _previewTargetDragging = false;
                evt.Use(); SceneView.RepaintAll(); return;
            }

            ProjectileDefinitionDetailView detail = _definition()?.detail;
            ProjectileRequestDetailView request = _request()?.detail;
            if (detail == null && request == null) return;
            Handles.color = new Color(.2f, .75f, 1f, .95f);

            DrawPreviewTargetHandle(detail);

            if (request?.origin != null)
                DrawRequestOriginHandles(request.origin);
            if (detail != null)
                DrawDefinitionHandles(detail);

            if (evt.type == EventType.MouseUp)
            {
                if (_pending != null) CommitPendingForTests();
                if (_previewTargetDragging)
                {
                    _previewTargetDragging = false;
                    _setPreviewTarget?.Invoke(_previewTarget(), true);
                }
            }
        }

        private void DrawPreviewTargetHandle(ProjectileDefinitionDetailView detail)
        {
            if (_previewTarget == null || _setPreviewTarget == null) return;
            Vector3 current = _previewTarget();
            Handles.color = new Color(1f, .75f, .15f, .95f);
            EditorGUI.BeginChangeCheck();
            Vector3 next = Handles.PositionHandle(current, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                if (!_previewTargetDragging) _previewTargetBeforeDrag = current;
                _previewTargetDragging = true;
                _setPreviewTarget(next, false);
            }
            string label = _previewTargetLabel?.Invoke();
            if (!string.IsNullOrEmpty(label))
            {
                Handles.DrawDottedLine(Vector3.zero, current, 4f);
                Handles.Label(current + Vector3.up * .25f, label);
            }
        }

        private void DrawRequestOriginHandles(ProjectileSpawnOriginView origin)
        {
            const string localPath = "origin.localOffset";
            const string fallbackPath = "origin.fallbackRootOffset";
            Vector3 local = _pending?.fieldPath == localPath && _pending.vectorValue != null
                ? V(_pending.vectorValue) : V(origin.localOffset);
            Vector3 fallback = _pending?.fieldPath == fallbackPath && _pending.vectorValue != null
                ? V(_pending.vectorValue) : V(origin.fallbackRootOffset);

            Handles.color = new Color(.2f, .8f, 1f, .95f);
            Handles.Label(local + Vector3.up * .12f, "Socket Local Offset");
            EditorGUI.BeginChangeCheck();
            Vector3 nextLocal = Handles.PositionHandle(local, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
                _pending = VectorEdit(localPath, nextLocal);

            Handles.color = new Color(1f, .55f, .15f, .95f);
            Handles.Label(fallback + Vector3.up * .12f, "Fallback Root Offset");
            EditorGUI.BeginChangeCheck();
            Vector3 nextFallback = Handles.PositionHandle(fallback, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
                _pending = VectorEdit(fallbackPath, nextFallback);
        }

        private void DrawDefinitionHandles(ProjectileDefinitionDetailView detail)
        {
            Vector3 center = Vector3.zero;
            ProjectileMotionView motion = detail.motion;
            ProjectileImpactView impact = detail.impact;
            ProjectileCollisionView collision = detail.collision;
            if (collision != null)
            {
                EditorGUI.BeginChangeCheck();
                float value = Handles.RadiusHandle(Quaternion.identity, center, _pending?.fieldPath == "collision.radius" ? _pending.floatValue : collision.radius);
                if (EditorGUI.EndChangeCheck()) _pending = FloatEdit("collision.radius", Math.Max(0f, value));
            }
            if (impact != null && string.Equals(impact.policyKind, "AreaPulseOnImpact", StringComparison.Ordinal))
            {
                EditorGUI.BeginChangeCheck();
                float value = Handles.RadiusHandle(Quaternion.identity, center, _pending?.fieldPath == "impact.areaRadius" ? _pending.floatValue : impact.areaRadius);
                if (EditorGUI.EndChangeCheck()) _pending = FloatEdit("impact.areaRadius", Math.Max(0f, value));
            }
            if (motion == null) return;
            if (string.Equals(motion.kind, "ArcToTarget", StringComparison.Ordinal)) DrawScalar("motion.arcHeight", motion.arcHeight, center, Vector3.up);
            else if (string.Equals(motion.kind, "SwayForward", StringComparison.Ordinal)) DrawScalar("motion.lateralAmplitude", motion.lateralAmplitude, center, Vector3.forward);
            else if (string.Equals(motion.kind, "BezierToTarget", StringComparison.Ordinal))
            {
                DrawVector("motion.bezierControlOffsetA", motion.bezierControlOffsetA, center);
                DrawVector("motion.bezierControlOffsetB", motion.bezierControlOffsetB, center);
            }
        }

        private void DrawScalar(string path, float current, Vector3 center, Vector3 direction)
        {
            float value = _pending?.fieldPath == path ? _pending.floatValue : current;
            EditorGUI.BeginChangeCheck();
            Vector3 next = Handles.Slider(center + direction * value, direction);
            if (EditorGUI.EndChangeCheck()) _pending = FloatEdit(path, Math.Max(0f, Vector3.Dot(next - center, direction)));
        }

        private void DrawVector(string path, ProjectileVector3View current, Vector3 center)
        {
            Vector3 value = _pending?.fieldPath == path && _pending.vectorValue != null ? V(_pending.vectorValue) : V(current);
            EditorGUI.BeginChangeCheck();
            Vector3 next = Handles.PositionHandle(center + value, Quaternion.identity) - center;
            if (EditorGUI.EndChangeCheck()) _pending = VectorEdit(path, next);
        }

        private static ProjectileAuthoringFieldEditView FloatEdit(string path, float value) => new ProjectileAuthoringFieldEditView { fieldPath = path, valueKind = "Float", floatValue = value };
        private static ProjectileAuthoringFieldEditView VectorEdit(string path, Vector3 value) => new ProjectileAuthoringFieldEditView { fieldPath = path, valueKind = "Vector", vectorValue = new ProjectileVector3View { x = value.x, y = value.y, z = value.z } };
        private static Vector3 V(ProjectileVector3View value) => value == null ? Vector3.zero : new Vector3(value.x, value.y, value.z);
    }
}

using System;
using UnityEditor;
using UnityEngine;

namespace MxFramework.CharacterCloth.Magica.Unity.Editor
{
    internal sealed class MagicaCharacterClothSceneViewEdit : IDisposable
    {
        private readonly MagicaCharacterClothAuthoringSession _session;
        private readonly Func<Transform> _bodyModel;
        private readonly Func<int> _clothIndex;
        private readonly Func<int> _colliderIndex;
        private readonly Func<bool> _showOnlySelected;
        private readonly Func<bool> _suppressEditing;
        private readonly Action _changed;
        private MagicaCharacterClothAuthoringCollider _beforeDrag;
        private bool _registered;

        internal MagicaCharacterClothSceneViewEdit(
            MagicaCharacterClothAuthoringSession session,
            Func<Transform> bodyModel,
            Func<int> clothIndex,
            Func<int> colliderIndex,
            Func<bool> showOnlySelected,
            Func<bool> suppressEditing,
            Action changed)
        {
            _session = session;
            _bodyModel = bodyModel;
            _clothIndex = clothIndex;
            _colliderIndex = colliderIndex;
            _showOnlySelected = showOnlySelected;
            _suppressEditing = suppressEditing;
            _changed = changed;
        }

        internal void Begin()
        {
            if (_registered) return;
            _registered = true;
            SceneView.duringSceneGui += OnSceneGui;
            SceneView.RepaintAll();
        }

        internal void End(bool cancel)
        {
            if (cancel) CancelDrag();
            if (!_registered) return;
            _registered = false;
            SceneView.duringSceneGui -= OnSceneGui;
            SceneView.RepaintAll();
        }

        public void Dispose() => End(true);

        private void OnSceneGui(SceneView sceneView)
        {
            if (_suppressEditing?.Invoke() == true) return;
            Transform body = _bodyModel();
            MagicaCharacterClothAuthoringCollider collider = SelectedCollider();
            if (body == null || collider == null) return;
            Transform parent = MagicaCharacterClothAuthoringValidator.FindTransform(body, collider.parentBonePath, out bool ambiguous);
            if (parent == null || ambiguous) return;

            if (!_showOnlySelected()) DrawOtherColliderOutlines(body, collider);

            Event evt = Event.current;
            if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape)
            {
                CancelDrag();
                evt.Use();
                return;
            }

            Vector3 localPosition = V(collider.localPosition);
            Quaternion localRotation = Quaternion.Euler(V(collider.localRotationEuler));
            Vector3 worldPosition = parent.TransformPoint(localPosition);
            Quaternion worldRotation = parent.rotation * localRotation;
            using (new Handles.DrawingScope(new Color(0.05f, 1f, 0.35f, 0.95f), Matrix4x4.identity))
            {
                DrawOutline(collider, worldPosition, worldRotation);
                EditorGUI.BeginChangeCheck();
                Vector3 nextPosition = Handles.PositionHandle(worldPosition, worldRotation);
                Quaternion nextRotation = Handles.RotationHandle(worldRotation, worldPosition);
                float nextRadius = Mathf.Max(0.001f, collider.radius);
                float nextEndRadius = collider.endRadius;
                float nextLength = collider.length;
                if (string.Equals(collider.type, "Capsule", StringComparison.Ordinal))
                    DrawCapsuleHandles(collider, nextPosition, nextRotation, ref nextRadius, ref nextEndRadius, ref nextLength);
                else
                    nextRadius = Handles.RadiusHandle(Quaternion.identity, nextPosition, nextRadius);
                if (EditorGUI.EndChangeCheck())
                {
                    if (_beforeDrag == null) _beforeDrag = CloneCollider(collider);
                    collider.localPosition = FromPosition(parent.InverseTransformPoint(nextPosition));
                    collider.localRotationEuler = FromRotation((Quaternion.Inverse(parent.rotation) * nextRotation).eulerAngles);
                    collider.radius = Mathf.Max(0.001f, nextRadius);
                    if (string.Equals(collider.type, "Capsule", StringComparison.Ordinal))
                    {
                        collider.endRadius = Mathf.Max(0.001f, nextEndRadius);
                        collider.length = Mathf.Max(0.001f, nextLength);
                    }
                    _changed?.Invoke();
                }
            }
            if (evt.type == EventType.MouseUp && _beforeDrag != null) CommitDrag(collider);
        }

        private static void DrawOutline(MagicaCharacterClothAuthoringCollider collider, Vector3 position, Quaternion rotation)
        {
            if (string.Equals(collider.type, "Sphere", StringComparison.Ordinal))
            {
                Handles.DrawWireDisc(position, Vector3.up, collider.radius);
                Handles.DrawWireDisc(position, Vector3.right, collider.radius);
                Handles.DrawWireDisc(position, Vector3.forward, collider.radius);
                return;
            }
            DrawCapsuleOutline(collider, position, rotation);
        }

        private void DrawOtherColliderOutlines(Transform body, MagicaCharacterClothAuthoringCollider selected)
        {
            MagicaCharacterClothAuthoringEntry[] cloths = _session.document?.cloths ?? Array.Empty<MagicaCharacterClothAuthoringEntry>();
            using (new Handles.DrawingScope(new Color(0.1f, 0.75f, 1f, 0.38f), Matrix4x4.identity))
            {
                for (int i = 0; i < cloths.Length; i++)
                {
                    MagicaCharacterClothAuthoringCollider[] colliders = cloths[i]?.colliders ?? Array.Empty<MagicaCharacterClothAuthoringCollider>();
                    for (int j = 0; j < colliders.Length; j++)
                    {
                        MagicaCharacterClothAuthoringCollider collider = colliders[j];
                        if (collider == null || ReferenceEquals(collider, selected)) continue;
                        Transform parent = MagicaCharacterClothAuthoringValidator.FindTransform(body, collider.parentBonePath, out bool ambiguous);
                        if (parent == null || ambiguous) continue;
                        Vector3 position = parent.TransformPoint(V(collider.localPosition));
                        Quaternion rotation = parent.rotation * Quaternion.Euler(V(collider.localRotationEuler));
                        DrawOutline(collider, position, rotation);
                    }
                }
            }
        }

        private static void DrawCapsuleOutline(MagicaCharacterClothAuthoringCollider collider, Vector3 position, Quaternion rotation)
        {
            Vector3 axis = Axis(collider.direction, rotation);
            PerpendicularAxes(collider.direction, rotation, axis, out Vector3 sideA, out Vector3 sideB);
            float startRadius = Mathf.Max(0.001f, collider.radius);
            float endRadius = EffectiveEndRadius(collider);
            float length = Mathf.Max(0.001f, collider.length);
            Vector3 startCenter = position + axis * MagicaCharacterClothCapsuleGeometry.StartCenterOffset(length, startRadius);
            Vector3 endCenter = position - axis * MagicaCharacterClothCapsuleGeometry.EndCenterOffset(length, endRadius);

            DrawWireSphere(startCenter, axis, sideA, sideB, startRadius);
            if ((endCenter - startCenter).sqrMagnitude > 0.0000000001f || !Mathf.Approximately(startRadius, endRadius))
                DrawWireSphere(endCenter, axis, sideA, sideB, endRadius);

            for (int degrees = 0; degrees < 360; degrees += 45)
            {
                float radians = degrees * Mathf.Deg2Rad;
                Vector3 radial = sideA * Mathf.Cos(radians) + sideB * Mathf.Sin(radians);
                Handles.DrawLine(startCenter + radial * startRadius, endCenter + radial * endRadius);
            }
        }

        private static void DrawWireSphere(Vector3 center, Vector3 axis, Vector3 sideA, Vector3 sideB, float radius)
        {
            Handles.DrawWireDisc(center, axis, radius);
            Handles.DrawWireDisc(center, sideA, radius);
            Handles.DrawWireDisc(center, sideB, radius);
        }

        private static void DrawCapsuleHandles(
            MagicaCharacterClothAuthoringCollider collider,
            Vector3 position,
            Quaternion rotation,
            ref float startRadius,
            ref float endRadius,
            ref float length)
        {
            Vector3 axis = Axis(collider.direction, rotation);
            PerpendicularAxes(collider.direction, rotation, axis, out Vector3 radial, out _);
            startRadius = Mathf.Max(0.001f, startRadius);
            endRadius = Mathf.Max(0.001f, endRadius > 0f ? endRadius : startRadius);
            length = Mathf.Max(0.001f, length);

            Vector3 startCenter = position + axis * MagicaCharacterClothCapsuleGeometry.StartCenterOffset(length, startRadius);
            Vector3 endCenter = position - axis * MagicaCharacterClothCapsuleGeometry.EndCenterOffset(length, endRadius);
            float startHandleSize = HandleUtility.GetHandleSize(startCenter) * 0.055f;
            float endHandleSize = HandleUtility.GetHandleSize(endCenter) * 0.055f;
            startRadius = Mathf.Max(0.001f, Handles.ScaleSlider(
                startRadius, startCenter, radial, Quaternion.identity, startHandleSize, 0f));
            endRadius = Mathf.Max(0.001f, Handles.ScaleSlider(
                endRadius, endCenter, -radial, Quaternion.identity, endHandleSize, 0f));

            float half = length * 0.5f;
            Vector3 startTip = position + axis * half;
            Vector3 endTip = position - axis * half;
            float lengthHandleSize = HandleUtility.GetHandleSize(startTip) * 0.065f;
            Vector3 nextStartTip = Handles.Slider(startTip, axis, lengthHandleSize, Handles.ConeHandleCap, 0f);
            Vector3 nextEndTip = Handles.Slider(endTip, -axis, lengthHandleSize, Handles.ConeHandleCap, 0f);
            length = Mathf.Max(0.001f, Vector3.Dot(nextStartTip - nextEndTip, axis));
        }

        private static void PerpendicularAxes(
            string direction,
            Quaternion rotation,
            Vector3 axis,
            out Vector3 sideA,
            out Vector3 sideB)
        {
            Vector3 localUp = direction == "Y" ? Vector3.forward : Vector3.up;
            sideA = (rotation * localUp).normalized;
            sideB = Vector3.Cross(axis, sideA).normalized;
        }

        private void CommitDrag(MagicaCharacterClothAuthoringCollider current)
        {
            MagicaCharacterClothAuthoringCollider final = CloneCollider(current);
            Copy(_beforeDrag, current);
            Undo.RecordObject(_session, "Edit Character Cloth Collider");
            Copy(final, current);
            EditorUtility.SetDirty(_session);
            _beforeDrag = null;
            _changed?.Invoke();
        }

        private void CancelDrag()
        {
            if (_beforeDrag == null) return;
            MagicaCharacterClothAuthoringCollider current = SelectedCollider();
            if (current != null) Copy(_beforeDrag, current);
            _beforeDrag = null;
            _changed?.Invoke();
        }

        private MagicaCharacterClothAuthoringCollider SelectedCollider()
        {
            MagicaCharacterClothAuthoringEntry[] cloths = _session.document?.cloths;
            int ci = _clothIndex();
            if (cloths == null || ci < 0 || ci >= cloths.Length) return null;
            MagicaCharacterClothAuthoringCollider[] colliders = cloths[ci]?.colliders;
            int xi = _colliderIndex();
            return colliders != null && xi >= 0 && xi < colliders.Length ? colliders[xi] : null;
        }

        private static MagicaCharacterClothAuthoringCollider CloneCollider(MagicaCharacterClothAuthoringCollider value)
        {
            return new MagicaCharacterClothAuthoringCollider
            {
                id = value.id, type = value.type, parentBonePath = value.parentBonePath,
                localPosition = From(V(value.localPosition)),
                localRotationEuler = From(V(value.localRotationEuler)),
                radius = value.radius, endRadius = value.endRadius, length = value.length, direction = value.direction,
                extensionData = value.extensionData,
            };
        }

        private static void Copy(MagicaCharacterClothAuthoringCollider source, MagicaCharacterClothAuthoringCollider target)
        {
            target.id = source.id; target.type = source.type; target.parentBonePath = source.parentBonePath;
            target.localPosition = From(V(source.localPosition)); target.localRotationEuler = From(V(source.localRotationEuler));
            target.radius = source.radius; target.endRadius = source.endRadius; target.length = source.length; target.direction = source.direction;
            target.extensionData = source.extensionData;
        }

        private static Vector3 Axis(string direction, Quaternion rotation) => rotation * (direction == "X" ? Vector3.right : direction == "Z" ? Vector3.forward : Vector3.up);
        private static float EffectiveEndRadius(MagicaCharacterClothAuthoringCollider collider) =>
            Mathf.Max(0.001f, collider.endRadius > 0f ? collider.endRadius : collider.radius);
        private static Vector3 V(MagicaCharacterClothAuthoringVector value) => value == null ? Vector3.zero : new Vector3(value.x, value.y, value.z);
        private static MagicaCharacterClothAuthoringVector From(Vector3 value) => new MagicaCharacterClothAuthoringVector(value.x, value.y, value.z);
        private static MagicaCharacterClothAuthoringVector FromPosition(Vector3 value) => new MagicaCharacterClothAuthoringVector(
            MagicaCharacterClothAuthoringNormalization.NormalizePosition(value.x),
            MagicaCharacterClothAuthoringNormalization.NormalizePosition(value.y),
            MagicaCharacterClothAuthoringNormalization.NormalizePosition(value.z));
        private static MagicaCharacterClothAuthoringVector FromRotation(Vector3 value) => new MagicaCharacterClothAuthoringVector(
            MagicaCharacterClothAuthoringNormalization.NormalizeRotation(value.x),
            MagicaCharacterClothAuthoringNormalization.NormalizeRotation(value.y),
            MagicaCharacterClothAuthoringNormalization.NormalizeRotation(value.z));
    }
}

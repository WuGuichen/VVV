using System;
using System.Collections.Generic;
using MxFramework.Combat.Authoring;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Combat.Editor
{
    [InitializeOnLoad]
    internal static class CombatGizmoDrawer
    {
        private const float DefaultMarkerRadius = 0.08f;
        private const float DefaultBodyRadius = 0.45f;
        private const float DefaultColliderRadius = 0.22f;
        private const float FixedRawScale = 1000000f;
        private const float LabelVerticalSpacing = 3f;
        private const float LabelHorizontalPadding = 6f;
        private const float LabelVerticalPadding = 3f;
        private const int RawScale = 1000000;

        private static readonly Color ActorColor = new Color(0.0f, 0.85f, 0.95f, 1f);
        private static readonly Color BodyColor = new Color(0.0f, 0.65f, 0.75f, 1f);
        private static readonly Color HurtboxColor = new Color(0.25f, 0.45f, 1f, 1f);
        private static readonly Color HitboxColor = new Color(1f, 0.55f, 0.1f, 1f);
        private static readonly Color TraceRootColor = new Color(1f, 0.9f, 0.05f, 1f);
        private static readonly Color TraceTipColor = new Color(1f, 0.2f, 0.05f, 1f);
        private static readonly Color MissingColor = new Color(1f, 0.1f, 0.1f, 1f);
        private static readonly Color SelectedColor = Color.white;

        private static readonly List<CombatActorBindingData> ActorBuffer = new List<CombatActorBindingData>(16);
        private static readonly List<CombatShapeAuthoringData> ShapeBuffer = new List<CombatShapeAuthoringData>(64);
        private static readonly List<CombatWeaponTraceAuthoringData> TraceBuffer = new List<CombatWeaponTraceAuthoringData>(32);
        private static readonly List<CombatMarkerBindingData> MarkerBuffer = new List<CombatMarkerBindingData>(64);
        private static readonly List<CombatColliderBindingData> ColliderBuffer = new List<CombatColliderBindingData>(32);
        private static readonly List<Transform> TransformBuffer = new List<Transform>(64);
        private static readonly List<Rect> LabelRects = new List<Rect>(64);
        private static readonly Dictionary<string, Transform> MarkerMap = new Dictionary<string, Transform>(StringComparer.Ordinal);

        private static GUIStyle _labelStyle;
        private static GUIStyle _labelBackgroundStyle;

        static CombatGizmoDrawer()
        {
            SceneView.duringSceneGui -= OnSceneGui;
            SceneView.duringSceneGui += OnSceneGui;
            Selection.selectionChanged -= OnSelectionChanged;
            Selection.selectionChanged += OnSelectionChanged;
        }

        private static void OnSelectionChanged()
        {
            SceneView.RepaintAll();
        }

        private static void OnSceneGui(SceneView sceneView)
        {
            CombatActionAuthoringAsset actionAsset = CombatAuthoringSceneState.ActionAsset;
            CombatSceneBindingAsset bindingAsset = CombatAuthoringSceneState.SceneBindingAsset;
            if (actionAsset == null && bindingAsset == null)
            {
                return;
            }

            CombatAuthoringVisibility visibility = CombatAuthoringSceneState.Visibility;
            int frame = CombatAuthoringSceneState.Frame;
            LabelRects.Clear();
            BuildMarkerMap(bindingAsset);

            if (bindingAsset != null)
            {
                DrawSceneBinding(bindingAsset, visibility);
            }

            if (actionAsset != null)
            {
                DrawActionPreview(actionAsset, frame, visibility);
            }
        }

        private static void DrawSceneBinding(CombatSceneBindingAsset bindingAsset, CombatAuthoringVisibility visibility)
        {
            ActorBuffer.Clear();
            if (bindingAsset.Actors != null)
            {
                ActorBuffer.AddRange(bindingAsset.Actors);
            }

            ActorBuffer.Sort(CompareActors);
            for (int i = 0; i < ActorBuffer.Count; i++)
            {
                CombatActorBindingData actor = ActorBuffer[i];
                Transform actorTransform = ResolveMarker(actor.MarkerId);
                if (visibility.Actor)
                {
                    DrawActor(actor, actorTransform, visibility.Labels);
                }

                if (visibility.Body)
                {
                    DrawBody(actor, actorTransform, visibility.Labels);
                }

                if (!visibility.Collider || actor.Colliders == null)
                {
                    continue;
                }

                ColliderBuffer.Clear();
                ColliderBuffer.AddRange(actor.Colliders);
                ColliderBuffer.Sort(CompareColliders);
                for (int colliderIndex = 0; colliderIndex < ColliderBuffer.Count; colliderIndex++)
                {
                    DrawBindingCollider(actor, ColliderBuffer[colliderIndex], visibility.Labels);
                }
            }
        }

        private static void DrawActionPreview(CombatActionAuthoringAsset actionAsset, int frame, CombatAuthoringVisibility visibility)
        {
            if (visibility.Collider)
            {
                DrawShapes(actionAsset, actionAsset.Hitboxes, frame, HitboxColor, "Hitbox", visibility.Labels);
                DrawShapes(actionAsset, actionAsset.Hurtboxes, frame, HurtboxColor, "Hurtbox", visibility.Labels);
            }

            if (visibility.Trace)
            {
                DrawTraces(actionAsset, frame, visibility.Labels);
            }
        }

        private static void DrawActor(CombatActorBindingData actor, Transform actorTransform, bool drawLabel)
        {
            if (actorTransform == null)
            {
                DrawMissingLabel("Actor " + actor.EntityId + " marker missing: " + actor.MarkerId);
                return;
            }

            using (new Handles.DrawingScope(ActorColor, actorTransform.localToWorldMatrix))
            {
                Handles.SphereHandleCap(0, Vector3.zero, Quaternion.identity, DefaultMarkerRadius, EventType.Repaint);
                Handles.ArrowHandleCap(0, Vector3.zero, Quaternion.identity, 0.35f, EventType.Repaint);
            }

            if (drawLabel)
            {
                DrawLabel(actorTransform.position + Vector3.up * 0.72f, "Entity " + actor.EntityId);
            }
        }

        private static void DrawBody(CombatActorBindingData actor, Transform actorTransform, bool drawLabel)
        {
            if (actorTransform == null)
            {
                return;
            }

            Handles.color = BodyColor;
            DrawWireSphere(actorTransform.position, DefaultBodyRadius);
            Handles.DrawLine(actorTransform.position, actorTransform.position + actorTransform.forward * DefaultBodyRadius);
            if (drawLabel)
            {
                DrawLabel(actorTransform.position + Vector3.up * (DefaultBodyRadius + 0.18f), "Body " + actor.BodyId);
            }
        }

        private static void DrawBindingCollider(CombatActorBindingData actor, CombatColliderBindingData collider, bool drawLabel)
        {
            Transform colliderTransform = ResolveMarker(collider.MarkerId);
            if (colliderTransform == null)
            {
                DrawMissingLabel("Collider " + collider.ColliderId + " marker missing: " + collider.MarkerId);
                return;
            }

            using (new Handles.DrawingScope(BodyColor, colliderTransform.localToWorldMatrix))
            {
                Handles.DrawWireCube(Vector3.zero, Vector3.one * (DefaultColliderRadius * 2f));
            }

            if (drawLabel)
            {
                DrawLabel(colliderTransform.position + Vector3.up * 0.24f, "Collider " + collider.ColliderId);
            }
        }

        private static void DrawShapes(
            CombatActionAuthoringAsset actionAsset,
            CombatShapeAuthoringData[] shapes,
            int frame,
            Color color,
            string labelPrefix,
            bool drawLabel)
        {
            ShapeBuffer.Clear();
            if (shapes != null)
            {
                ShapeBuffer.AddRange(shapes);
            }

            ShapeBuffer.Sort(CompareShapes);
            for (int i = 0; i < ShapeBuffer.Count; i++)
            {
                CombatShapeAuthoringData shape = ShapeBuffer[i];
                if (!ContainsFrame(shape.FrameRange, frame))
                {
                    continue;
                }

                Transform markerTransform = ResolveMarker(shape.MarkerId);
                if (markerTransform == null)
                {
                    DrawMissingLabel(labelPrefix + " " + shape.TrackId + " marker missing: " + shape.MarkerId);
                    continue;
                }

                float radius = ToPreviewRadius(shape.RadiusRaw, DefaultColliderRadius);
                float height = ToPreviewHeight(shape.HeightRaw, radius);
                Vector3 center = markerTransform.TransformPoint(shape.LocalCenter);
                Handles.color = color;
                DrawShapeOutline(shape.ShapeKind, markerTransform, center, radius, height);
                DrawSelectedShapeHandle(actionAsset, shape, markerTransform, center, labelPrefix, radius, height);
                if (drawLabel)
                {
                    DrawLabel(center + Vector3.up * 0.34f, labelPrefix + " " + shape.TrackId);
                }
            }
        }

        private static void DrawTraces(CombatActionAuthoringAsset actionAsset, int frame, bool drawLabel)
        {
            TraceBuffer.Clear();
            if (actionAsset.WeaponTraces != null)
            {
                TraceBuffer.AddRange(actionAsset.WeaponTraces);
            }

            TraceBuffer.Sort(CompareTraces);
            for (int i = 0; i < TraceBuffer.Count; i++)
            {
                CombatWeaponTraceAuthoringData trace = TraceBuffer[i];
                if (!ContainsFrame(trace.FrameRange, frame))
                {
                    continue;
                }

                Transform root = ResolveMarker(trace.RootMarkerId);
                Transform tip = ResolveMarker(trace.TipMarkerId);
                if (root == null || tip == null)
                {
                    DrawMissingLabel("Trace " + trace.TraceId + " marker missing: " + trace.RootMarkerId + " -> " + trace.TipMarkerId);
                    continue;
                }

                float radius = ToPreviewRadius(trace.RadiusRaw, DefaultMarkerRadius);
                Handles.color = TraceRootColor;
                DrawWireSphere(root.position, radius);
                Handles.color = TraceTipColor;
                DrawWireSphere(tip.position, radius);
                Handles.color = Color.Lerp(TraceRootColor, TraceTipColor, 0.5f);
                Handles.DrawAAPolyLine(4f, root.position, tip.position);
                DrawTraceTube(root.position, tip.position, radius);
                if (drawLabel)
                {
                    DrawLabel(Vector3.Lerp(root.position, tip.position, 0.5f), "Trace " + trace.TraceId);
                }
            }
        }

        private static void DrawShapeOutline(
            CombatAuthoringShapeKind kind,
            Transform markerTransform,
            Vector3 center,
            float radius,
            float height)
        {
            switch (kind)
            {
                case CombatAuthoringShapeKind.Capsule:
                    DrawWireCapsule(center, markerTransform.rotation, radius, height);
                    break;
                case CombatAuthoringShapeKind.Aabb:
                    using (new Handles.DrawingScope(Handles.color, Matrix4x4.TRS(center, markerTransform.rotation, markerTransform.lossyScale)))
                    {
                        Handles.DrawWireCube(Vector3.zero, Vector3.one * (radius * 2f));
                    }
                    break;
                case CombatAuthoringShapeKind.Sector:
                    DrawWireSector(center, markerTransform.rotation, radius, 90f);
                    break;
                case CombatAuthoringShapeKind.Sphere:
                default:
                    DrawWireSphere(center, radius);
                    break;
            }
        }

        private static void DrawWireSphere(Vector3 center, float radius)
        {
            Handles.DrawWireDisc(center, Vector3.up, radius);
            Handles.DrawWireDisc(center, Vector3.right, radius);
            Handles.DrawWireDisc(center, Vector3.forward, radius);
        }

        private static void DrawWireCapsule(Vector3 center, Quaternion rotation, float radius, float height)
        {
            Vector3 up = rotation * Vector3.up;
            Vector3 right = rotation * Vector3.right;
            Vector3 forward = rotation * Vector3.forward;
            float halfLine = Mathf.Max(0f, height * 0.5f - radius);
            Vector3 top = center + up * halfLine;
            Vector3 bottom = center - up * halfLine;

            Handles.DrawWireDisc(top, up, radius);
            Handles.DrawWireDisc(bottom, up, radius);
            Handles.DrawLine(top + right * radius, bottom + right * radius);
            Handles.DrawLine(top - right * radius, bottom - right * radius);
            Handles.DrawLine(top + forward * radius, bottom + forward * radius);
            Handles.DrawLine(top - forward * radius, bottom - forward * radius);
            Handles.DrawWireArc(top, right, forward, 180f, radius);
            Handles.DrawWireArc(top, forward, -right, 180f, radius);
            Handles.DrawWireArc(bottom, right, -forward, 180f, radius);
            Handles.DrawWireArc(bottom, forward, right, 180f, radius);
        }

        private static void DrawWireSector(Vector3 center, Quaternion rotation, float radius, float angle)
        {
            Vector3 up = rotation * Vector3.up;
            Vector3 forward = rotation * Vector3.forward;
            Vector3 left = Quaternion.AngleAxis(-angle * 0.5f, up) * forward;
            Vector3 right = Quaternion.AngleAxis(angle * 0.5f, up) * forward;
            Handles.DrawWireArc(center, up, left, angle, radius);
            Handles.DrawLine(center, center + left * radius);
            Handles.DrawLine(center, center + right * radius);
        }

        private static void DrawTraceTube(Vector3 root, Vector3 tip, float radius)
        {
            Vector3 direction = tip - root;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Vector3 normal = Vector3.Cross(direction.normalized, Vector3.up);
            if (normal.sqrMagnitude < 0.0001f)
            {
                normal = Vector3.Cross(direction.normalized, Vector3.right);
            }

            normal.Normalize();
            Handles.DrawLine(root + normal * radius, tip + normal * radius);
            Handles.DrawLine(root - normal * radius, tip - normal * radius);
        }

        private static void BuildMarkerMap(CombatSceneBindingAsset bindingAsset)
        {
            MarkerMap.Clear();
            MarkerBuffer.Clear();
            if (bindingAsset?.Markers == null)
            {
                return;
            }

            MarkerBuffer.AddRange(bindingAsset.Markers);
            MarkerBuffer.Sort(CompareMarkers);
            for (int i = 0; i < MarkerBuffer.Count; i++)
            {
                CombatMarkerBindingData marker = MarkerBuffer[i];
                if (string.IsNullOrEmpty(marker.MarkerId) || MarkerMap.ContainsKey(marker.MarkerId))
                {
                    continue;
                }

                MarkerMap.Add(marker.MarkerId, ResolveTransformPath(marker.TargetPath));
            }
        }

        private static Transform ResolveMarker(string markerId)
        {
            if (string.IsNullOrEmpty(markerId))
            {
                return null;
            }

            MarkerMap.TryGetValue(markerId, out Transform markerTransform);
            return markerTransform;
        }

        private static Transform ResolveTransformPath(string targetPath)
        {
            if (string.IsNullOrEmpty(targetPath))
            {
                return null;
            }

            GameObject direct = GameObject.Find(targetPath);
            if (direct != null)
            {
                return direct.transform;
            }

            TransformBuffer.Clear();
            Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate == null
                    || EditorUtility.IsPersistent(candidate)
                    || (candidate.hideFlags & HideFlags.HideInHierarchy) != 0)
                {
                    continue;
                }

                string candidatePath = GetHierarchyPath(candidate);
                if (string.Equals(candidatePath, targetPath, StringComparison.Ordinal)
                    || string.Equals(candidate.name, targetPath, StringComparison.Ordinal))
                {
                    TransformBuffer.Add(candidate);
                }
            }

            TransformBuffer.Sort(CompareTransformsByPath);
            return TransformBuffer.Count == 0 ? null : TransformBuffer[0];
        }

        private static string GetHierarchyPath(Transform transform)
        {
            string path = transform.name;
            Transform current = transform.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }

        private static bool ContainsFrame(CombatAuthoringFrameRange range, int frame)
        {
            return !range.IsEmpty && frame >= range.StartFrame && frame <= range.EndFrame;
        }

        private static float ToPreviewRadius(int radiusRaw, float defaultRadius)
        {
            if (radiusRaw <= 0)
            {
                return defaultRadius;
            }

            return Mathf.Max(0.01f, radiusRaw / FixedRawScale);
        }

        private static float ToPreviewHeight(int heightRaw, float radius)
        {
            float minimum = radius * 2f;
            if (heightRaw <= 0)
            {
                return Mathf.Max(minimum, radius * 3f);
            }

            return Mathf.Max(minimum, heightRaw / FixedRawScale);
        }

        private static int ToRadiusRaw(float radius)
        {
            return Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(0.01f, radius) * RawScale));
        }

        private static int ToHeightRaw(float height, float radius)
        {
            float minimum = Mathf.Max(0.02f, radius * 2f);
            return Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(minimum, height) * RawScale));
        }

        private static void DrawSelectedShapeHandle(
            CombatActionAuthoringAsset actionAsset,
            CombatShapeAuthoringData shape,
            Transform markerTransform,
            Vector3 center,
            string section,
            float radius,
            float height)
        {
            CombatAuthoringSelection selection = CombatAuthoringSceneState.Selection;
            if (actionAsset == null || markerTransform == null || !selection.Matches(section, shape.TrackId))
            {
                return;
            }

            SerializedObject serialized = new SerializedObject(actionAsset);
            serialized.Update();
            SerializedProperty property = serialized.FindProperty(selection.PropertyPath);
            SerializedProperty centerProperty = property?.FindPropertyRelative("localCenter");
            SerializedProperty radiusProperty = property?.FindPropertyRelative("radiusRaw");
            SerializedProperty heightProperty = property?.FindPropertyRelative("heightRaw");
            if (centerProperty == null || radiusProperty == null || heightProperty == null)
            {
                return;
            }

            using (new Handles.DrawingScope(SelectedColor))
            {
                DrawShapeOutline(shape.ShapeKind, markerTransform, center, radius, height);
                EditorGUI.BeginChangeCheck();
                Vector3 newCenter = Handles.PositionHandle(center, markerTransform.rotation);
                float newRadius = Handles.RadiusHandle(Quaternion.identity, newCenter, radius);
                float newHeight = height;
                if (shape.ShapeKind == CombatAuthoringShapeKind.Capsule)
                {
                    newHeight = DrawCapsuleHeightHandles(markerTransform, newCenter, newRadius, height);
                }

                if (!EditorGUI.EndChangeCheck())
                {
                    return;
                }

                Undo.RecordObject(actionAsset, "Edit Combat Shape Transform");
                centerProperty.vector3Value = markerTransform.InverseTransformPoint(newCenter);
                radiusProperty.intValue = ToRadiusRaw(newRadius);
                if (shape.ShapeKind == CombatAuthoringShapeKind.Capsule)
                {
                    heightProperty.intValue = ToHeightRaw(newHeight, newRadius);
                }

                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(actionAsset);
                SceneView.RepaintAll();
            }
        }

        private static float DrawCapsuleHeightHandles(Transform markerTransform, Vector3 center, float radius, float height)
        {
            Vector3 up = markerTransform.up;
            float halfLine = Mathf.Max(0f, height * 0.5f - radius);
            Vector3 top = center + up * halfLine;
            Vector3 bottom = center - up * halfLine;
            float topSize = HandleUtility.GetHandleSize(top) * 0.1f;
            float bottomSize = HandleUtility.GetHandleSize(bottom) * 0.1f;

            Vector3 newTop = Handles.Slider(top, up, topSize, Handles.ConeHandleCap, 0f);
            Vector3 newBottom = Handles.Slider(bottom, -up, bottomSize, Handles.ConeHandleCap, 0f);
            float projectedSpan = Mathf.Max(0f, Vector3.Dot(newTop - newBottom, up));
            return Mathf.Max(radius * 2f, projectedSpan + radius * 2f);
        }

        private static void DrawMissingLabel(string text)
        {
            if (Camera.current == null)
            {
                return;
            }

            Handles.color = MissingColor;
            DrawLabel(Camera.current.transform.position + Camera.current.transform.forward * 2f, text);
        }

        private static void DrawLabel(Vector3 position, string text)
        {
            if (Event.current == null || Event.current.type != EventType.Repaint)
            {
                return;
            }

            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(EditorStyles.boldLabel);
                _labelStyle.normal.textColor = Color.white;
            }

            if (_labelBackgroundStyle == null)
            {
                _labelBackgroundStyle = new GUIStyle(EditorStyles.helpBox);
                _labelBackgroundStyle.normal.textColor = Color.white;
            }

            Vector2 guiPoint = HandleUtility.WorldToGUIPoint(position);
            GUIContent content = new GUIContent(text);
            Vector2 size = _labelStyle.CalcSize(content);
            size.x += LabelHorizontalPadding * 2f;
            size.y += LabelVerticalPadding * 2f;

            Rect rect = new Rect(guiPoint.x - size.x * 0.5f, guiPoint.y - size.y * 0.5f, size.x, size.y);
            for (int i = 0; i < LabelRects.Count; i++)
            {
                if (!rect.Overlaps(LabelRects[i]))
                {
                    continue;
                }

                rect.y = LabelRects[i].yMax + LabelVerticalSpacing;
                i = -1;
            }

            LabelRects.Add(rect);
            Rect textRect = new Rect(
                rect.x + LabelHorizontalPadding,
                rect.y + LabelVerticalPadding,
                rect.width - LabelHorizontalPadding * 2f,
                rect.height - LabelVerticalPadding * 2f);

            Handles.BeginGUI();
            GUI.Box(rect, GUIContent.none, _labelBackgroundStyle);
            GUI.Label(textRect, content, _labelStyle);
            Handles.EndGUI();
        }

        private static int CompareActors(CombatActorBindingData left, CombatActorBindingData right)
        {
            int entityCompare = left.EntityId.CompareTo(right.EntityId);
            return entityCompare != 0 ? entityCompare : left.BodyId.CompareTo(right.BodyId);
        }

        private static int CompareColliders(CombatColliderBindingData left, CombatColliderBindingData right)
        {
            int idCompare = left.ColliderId.CompareTo(right.ColliderId);
            return idCompare != 0 ? idCompare : left.SourceOrder.CompareTo(right.SourceOrder);
        }

        private static int CompareShapes(CombatShapeAuthoringData left, CombatShapeAuthoringData right)
        {
            int trackCompare = left.TrackId.CompareTo(right.TrackId);
            if (trackCompare != 0)
            {
                return trackCompare;
            }

            int frameCompare = left.FrameRange.StartFrame.CompareTo(right.FrameRange.StartFrame);
            return frameCompare != 0 ? frameCompare : left.SourceOrder.CompareTo(right.SourceOrder);
        }

        private static int CompareTraces(CombatWeaponTraceAuthoringData left, CombatWeaponTraceAuthoringData right)
        {
            int traceCompare = left.TraceId.CompareTo(right.TraceId);
            if (traceCompare != 0)
            {
                return traceCompare;
            }

            int frameCompare = left.FrameRange.StartFrame.CompareTo(right.FrameRange.StartFrame);
            return frameCompare != 0 ? frameCompare : left.SourceOrder.CompareTo(right.SourceOrder);
        }

        private static int CompareMarkers(CombatMarkerBindingData left, CombatMarkerBindingData right)
        {
            int markerCompare = string.CompareOrdinal(left.MarkerId, right.MarkerId);
            if (markerCompare != 0)
            {
                return markerCompare;
            }

            return left.SourceOrder.CompareTo(right.SourceOrder);
        }

        private static int CompareTransformsByPath(Transform left, Transform right)
        {
            return string.CompareOrdinal(GetHierarchyPath(left), GetHierarchyPath(right));
        }
    }
}

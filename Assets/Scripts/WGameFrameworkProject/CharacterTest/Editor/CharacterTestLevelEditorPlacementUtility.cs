using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal static class CharacterTestLevelEditorPlacementUtility
    {
        private const string GridSnapEnabledPrefKey =
            "MxFramework.LevelEditor.CharacterTest.GridSnapEnabled";

        public static bool GridSnapEnabled
        {
            get => EditorPrefs.GetBool(GridSnapEnabledPrefKey, false);
            set => EditorPrefs.SetBool(GridSnapEnabledPrefKey, value);
        }

        public static Vector3 GridStep => EditorSnapSettings.move;

        public static bool TryResolveSurfacePosition(
            Vector2 guiPosition,
            out Vector3 position,
            out Vector3 normal)
        {
            if (HandleUtility.PlaceObject(guiPosition, out position, out normal))
            {
                position = ApplyHorizontalGrid(position, GridSnapEnabled);
                return true;
            }

            Ray ray = HandleUtility.GUIPointToWorldRay(guiPosition);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (!plane.Raycast(ray, out float distance))
            {
                position = default;
                normal = Vector3.up;
                return false;
            }

            position = ApplyHorizontalGrid(ray.GetPoint(distance), GridSnapEnabled);
            normal = Vector3.up;
            return true;
        }

        public static Vector3 ApplyHorizontalGrid(Vector3 position, bool enabled)
        {
            if (!enabled)
                return position;

            Vector3 step = EditorSnapSettings.move;
            if (Mathf.Abs(step.x) > Mathf.Epsilon)
                position.x = SnapCoordinate(position.x, Mathf.Abs(step.x));
            if (Mathf.Abs(step.z) > Mathf.Epsilon)
                position.z = SnapCoordinate(position.z, Mathf.Abs(step.z));
            return position;
        }

        public static Vector3 ResolvePivotPosition(
            GameObject source,
            Vector3 surfacePosition,
            float fallbackBottomOffset)
        {
            float bottomOffset = Mathf.Max(0f, fallbackBottomOffset);
            if (source != null && TryGetWorldBounds(source, out Bounds bounds))
                bottomOffset = Mathf.Max(0f, source.transform.position.y - bounds.min.y);

            surfacePosition.y += bottomOffset;
            return surfacePosition;
        }

        public static int AlignSelectionToGrid(Transform[] transforms, string undoName)
        {
            if (transforms == null || transforms.Length == 0)
                return 0;

            string label = string.IsNullOrWhiteSpace(undoName) ? "对齐到网格" : undoName.Trim();
            var objects = new UnityEngine.Object[transforms.Length];
            for (int i = 0; i < transforms.Length; i++)
                objects[i] = transforms[i];
            Undo.RecordObjects(objects, label);

            int changed = 0;
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform transform = transforms[i];
                if (transform == null)
                    continue;

                Vector3 snapped = ApplyHorizontalGrid(transform.position, enabled: true);
                if (snapped == transform.position)
                    continue;

                transform.position = snapped;
                EditorUtility.SetDirty(transform);
                changed++;
            }

            return changed;
        }

        public static string FormatGridStep()
        {
            Vector3 step = GridStep;
            return Math.Abs(step.x - step.z) <= 0.0001f
                ? step.x.ToString("0.###")
                : step.x.ToString("0.###") + " × " + step.z.ToString("0.###");
        }

        private static bool TryGetWorldBounds(GameObject source, out Bounds bounds)
        {
            Renderer[] renderers = source.GetComponentsInChildren<Renderer>(includeInactive: true);
            bool hasBounds = false;
            bounds = default;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds;
        }

        private static float SnapCoordinate(float value, float step)
        {
            return Mathf.Round(value / step) * step;
        }
    }

    internal static class CharacterTestLevelEditorMeshPreview
    {
        private static readonly Dictionary<PrimitiveType, PrimitivePreviewData> PrimitiveCache =
            new Dictionary<PrimitiveType, PrimitivePreviewData>();

        public static bool DrawGameObjectAtPosition(GameObject source, Vector3 position)
        {
            if (source == null)
                return false;

            Matrix4x4 translation = Matrix4x4.Translate(position - source.transform.position);
            MeshFilter[] filters = source.GetComponentsInChildren<MeshFilter>(includeInactive: true);
            bool drawn = false;
            for (int i = 0; i < filters.Length; i++)
            {
                MeshFilter filter = filters[i];
                if (filter == null
                    || filter.sharedMesh == null
                    || !filter.TryGetComponent(out MeshRenderer renderer)
                    || renderer == null
                    || !renderer.enabled
                    || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                drawn |= DrawMesh(
                    filter.sharedMesh,
                    translation * filter.transform.localToWorldMatrix,
                    renderer.sharedMaterials);
            }

            return drawn;
        }

        public static bool DrawPrimitive(
            CharacterTestMovableNaturalObjectShapeKind shapeKind,
            Vector3 position,
            Vector3 fullExtents)
        {
            PrimitiveType primitiveType = ToPrimitiveType(shapeKind);
            if (!TryGetPrimitivePreviewData(primitiveType, out PrimitivePreviewData preview))
                return false;

            return DrawMesh(
                preview.Mesh,
                Matrix4x4.TRS(position, Quaternion.identity, fullExtents),
                preview.Materials);
        }

        private static bool DrawMesh(Mesh mesh, Matrix4x4 matrix, Material[] materials)
        {
            if (mesh == null || materials == null || materials.Length == 0)
                return false;

            bool drawn = false;
            int subMeshCount = Mathf.Max(1, mesh.subMeshCount);
            for (int subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
            {
                Material material = materials[Mathf.Min(subMeshIndex, materials.Length - 1)];
                if (material == null || !material.SetPass(0))
                    continue;

                Graphics.DrawMeshNow(mesh, matrix, subMeshIndex);
                drawn = true;
            }

            return drawn;
        }

        private static bool TryGetPrimitivePreviewData(
            PrimitiveType primitiveType,
            out PrimitivePreviewData preview)
        {
            if (PrimitiveCache.TryGetValue(primitiveType, out preview))
                return preview.Mesh != null;

            GameObject primitive = GameObject.CreatePrimitive(primitiveType);
            primitive.hideFlags = HideFlags.HideAndDontSave;
            MeshFilter filter = primitive.GetComponent<MeshFilter>();
            MeshRenderer renderer = primitive.GetComponent<MeshRenderer>();
            preview = new PrimitivePreviewData(
                filter == null ? null : filter.sharedMesh,
                renderer == null ? null : renderer.sharedMaterials);
            UnityEngine.Object.DestroyImmediate(primitive);
            PrimitiveCache.Add(primitiveType, preview);
            return preview.Mesh != null;
        }

        private static PrimitiveType ToPrimitiveType(
            CharacterTestMovableNaturalObjectShapeKind shapeKind)
        {
            switch (shapeKind)
            {
                case CharacterTestMovableNaturalObjectShapeKind.Sphere:
                    return PrimitiveType.Sphere;
                case CharacterTestMovableNaturalObjectShapeKind.Capsule:
                    return PrimitiveType.Capsule;
                default:
                    return PrimitiveType.Cube;
            }
        }

        private readonly struct PrimitivePreviewData
        {
            public PrimitivePreviewData(Mesh mesh, Material[] materials)
            {
                Mesh = mesh;
                Materials = materials;
            }

            public Mesh Mesh { get; }

            public Material[] Materials { get; }
        }
    }
}

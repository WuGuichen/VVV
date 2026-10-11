using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal readonly struct VegetationSurfaceHit
    {
        internal readonly Vector3 Position;
        internal readonly Vector3 Normal;
        internal VegetationSurfaceHit(Vector3 position, Vector3 normal) { Position = position; Normal = normal; }
    }
    internal interface IVegetationSurfaceProvider
    {
        bool TryProject(Vector3 candidate, out VegetationSurfaceHit hit);
    }

    /// <summary>Authoring-only ray/mesh sampling on an explicit ground scope. A miss is always a miss.</summary>
    internal sealed class CharacterTestVegetationSurfaceSampler : IVegetationSurfaceProvider
    {
        private delegate bool MeshIntersection(Ray ray, Mesh mesh, Matrix4x4 matrix, out RaycastHit hit);
        private static readonly MeshIntersection Intersect = ResolveIntersection();
        private readonly MeshFilter[] _filters;
        private readonly TerrainCollider[] _terrains;
        private readonly Bounds _bounds;
        private readonly bool _hasBounds;
        private readonly float _minimumUp;
        internal string Issue { get; }

        internal CharacterTestVegetationSurfaceSampler(Transform root, float maximumSlope)
        {
            _minimumUp = Mathf.Cos(Mathf.Clamp(maximumSlope, 0, 89) * Mathf.Deg2Rad);
            _filters = root == null ? Array.Empty<MeshFilter>() : root.GetComponentsInChildren<MeshFilter>(false);
            _terrains = root == null ? Array.Empty<TerrainCollider>() : root.GetComponentsInChildren<TerrainCollider>(false);
            Bounds bounds = default; bool hasBounds = false;
            foreach (var filter in _filters)
            {
                if (filter.sharedMesh == null || !filter.TryGetComponent<Renderer>(out var renderer) || !renderer.enabled) continue;
                if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; } else bounds.Encapsulate(renderer.bounds);
            }
            foreach (var terrain in _terrains)
            {
                if (!terrain.enabled) continue;
                if (!hasBounds) { bounds = terrain.bounds; hasBounds = true; } else bounds.Encapsulate(terrain.bounds);
            }
            _bounds = bounds; _hasBounds = hasBounds;
            Issue = root == null ? "请选择地面范围；不使用空平面兜底。" :
                !hasBounds ? "该范围没有可用地面网格或 TerrainCollider。" :
                Intersect == null && _filters.Length > 0 ? "当前 Editor 不提供网格射线命中接口。" : string.Empty;
        }

        internal bool TryRay(Ray ray, out VegetationSurfaceHit hit)
        {
            hit = default;
            if (!_hasBounds || !string.IsNullOrEmpty(Issue)) return false;
            float nearest = float.PositiveInfinity; RaycastHit best = default; bool found = false;
            foreach (var filter in _filters)
            {
                if (filter == null || filter.sharedMesh == null || !filter.TryGetComponent<Renderer>(out var renderer) ||
                    !renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                    !renderer.bounds.IntersectRay(ray, out float entry) || entry > nearest) continue;
                if (Intersect(ray, filter.sharedMesh, filter.transform.localToWorldMatrix, out var sample) &&
                    sample.distance >= 0f && sample.distance < nearest)
                { nearest = sample.distance; best = sample; found = true; }
            }
            foreach (var terrain in _terrains)
            {
                if (terrain == null || !terrain.enabled || !terrain.gameObject.activeInHierarchy) continue;
                if (terrain.Raycast(ray, out var sample, nearest) && sample.distance < nearest)
                { nearest = sample.distance; best = sample; found = true; }
            }
            // Reject the nearest steep surface rather than painting through it onto hidden ground.
            if (!found || best.normal.sqrMagnitude < 0.5f || Vector3.Dot(best.normal.normalized, Vector3.up) < _minimumUp) return false;
            hit = new VegetationSurfaceHit(best.point, best.normal.normalized); return true;
        }

        public bool TryProject(Vector3 candidate, out VegetationSurfaceHit hit)
        {
            float top = _bounds.max.y + 10f;
            return TryRay(new Ray(new Vector3(candidate.x, top, candidate.z), Vector3.down), out hit);
        }

        private static MeshIntersection ResolveIntersection()
        {
            MethodInfo method = typeof(HandleUtility).GetMethod("IntersectRayMesh", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null, new[] { typeof(Ray), typeof(Mesh), typeof(Matrix4x4), typeof(RaycastHit).MakeByRefType() }, null);
            return method == null ? null : (MeshIntersection)Delegate.CreateDelegate(typeof(MeshIntersection), method);
        }
    }
}

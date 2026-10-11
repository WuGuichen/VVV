using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Owns geometry choice on explicitly authored three-tier LODGroups in a registered view.</summary>
    internal sealed class PresentationGeometryLodConsumer
    {
        private sealed class Binding
        {
            internal LODGroup Group;
            internal float High, Medium;
            internal int Applied = -1, Demand;
            internal bool RequiresFullPose;
            internal readonly Dictionary<int, int> CameraTiers = new Dictionary<int, int>();
        }
        private readonly List<Binding> _groups = new List<Binding>();
        internal int Count => _groups.Count;
        internal int AppliedTierAt(int index) => _groups[index].Applied;
        internal long Evaluations { get; private set; }
        internal long Transitions { get; private set; }

        /// <remarks>LowFreqAlloc at registration or explicit equipment invalidation only.</remarks>
        internal void Bind(GameObject root)
        {
            Clear();
            foreach (LODGroup group in root.GetComponentsInChildren<LODGroup>(true))
            {
                LOD[] levels = group.GetLODs();
                if (!group.enabled || levels.Length != 3 || !(levels[0].screenRelativeTransitionHeight > levels[1].screenRelativeTransitionHeight)
                    || !(levels[1].screenRelativeTransitionHeight > 0)) continue;
                var meshes = new HashSet<Mesh>(); bool valid = true, requiresFullPose = false; long previousTriangles = long.MaxValue;
                for (int tier = 0; tier < levels.Length; tier++)
                {
                    long triangles = 0;
                    if (levels[tier].renderers == null || levels[tier].renderers.Length == 0) { valid = false; break; }
                    foreach (Renderer renderer in levels[tier].renderers)
                    {
                        if (renderer == null || !renderer.transform.IsChildOf(root.transform)) { valid = false; break; }
                        Mesh mesh = renderer is SkinnedMeshRenderer skin ? skin.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
                        requiresFullPose |= renderer is SkinnedMeshRenderer && renderer.shadowCastingMode != ShadowCastingMode.Off;
                        // The custom triangle-shadow source remains the complete current-pose LOD0.
                        // Lower levels must explicitly opt out; this never rewrites casting or pose settings.
                        if (mesh == null || !meshes.Add(mesh) || (tier > 0 && renderer.shadowCastingMode != ShadowCastingMode.Off))
                        { valid = false; break; }
                        for (int sub = 0; sub < mesh.subMeshCount; sub++)
                        { if (mesh.GetTopology(sub) != MeshTopology.Triangles) valid = false; triangles += mesh.GetIndexCount(sub) / 3; }
                    }
                    if (triangles <= 0 || triangles >= previousTriangles) valid = false;
                    previousTriangles = triangles;
                    if (!valid) break;
                }
                if (valid) _groups.Add(new Binding { Group = group, High = levels[0].screenRelativeTransitionHeight,
                    Medium = levels[1].screenRelativeTransitionHeight, RequiresFullPose = requiresFullPose });
            }
        }
        internal void Begin(bool important)
        { foreach (Binding binding in _groups) binding.Demand = important || binding.RequiresFullPose ? 0 : 3; }
        internal void Observe(int cameraId, Matrix4x4 view, Matrix4x4 projection, bool orthographic)
        {
            foreach (Binding binding in _groups)
            {
                if (binding.Group == null || !(binding.Group.enabled && binding.Group.gameObject.activeInHierarchy) || binding.Demand == 0) continue;
                Transform transform = binding.Group.transform;
                Vector3 scale = transform.lossyScale;
                float radius = binding.Group.size * .5f * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
                float height = PresentationGeometryLodPolicy.ScreenHeight(view, projection,
                    transform.TransformPoint(binding.Group.localReferencePoint), radius, orthographic);
                int previous = binding.CameraTiers.TryGetValue(cameraId, out int level) ? level : -1;
                int tier = PresentationGeometryLodPolicy.Select(height, binding.High, binding.Medium,
                    previous, PresentationGeometryLodPolicy.DefaultHysteresis);
                binding.CameraTiers[cameraId] = tier; binding.Demand = Math.Min(binding.Demand, tier); Evaluations++;
            }
        }
        internal void Apply()
        {
            foreach (Binding binding in _groups)
            {
                int tier = binding.Demand == 3 ? 0 : binding.Demand;
                if (binding.Group == null || !(binding.Group.enabled && binding.Group.gameObject.activeInHierarchy) || binding.Applied == tier) continue;
                binding.Group.ForceLOD(tier); binding.Applied = tier; Transitions++;
            }
        }
        internal void RemoveCamera(int id) { foreach (Binding binding in _groups) binding.CameraTiers.Remove(id); }
        internal void Clear()
        {
            foreach (Binding binding in _groups) if (binding.Group != null && (binding.Group.enabled && binding.Group.gameObject.activeInHierarchy)) binding.Group.ForceLOD(-1);
            _groups.Clear();
        }
    }
}

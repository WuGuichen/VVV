using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.AI;

namespace MxFramework.Rendering.Editor
{
    /// <summary>Allocating offline demand snapshot using geometry and authored navigation;
    /// never the current camera, frame radiance, Static flags or moving actor poses.</summary>
    public sealed class MxSceneLightingProbeDemand
    {
        public MxSceneLightingProbeGeometry Geometry { get; }
        public Bounds[] NavigationBounds { get; }
        public MxSceneLightingProbeDemand(IReadOnlyList<Vector3> triangles, Bounds[] navigationBounds = null)
        {
            Geometry = triangles != null && triangles.Count > 0 ? new MxSceneLightingProbeGeometry(triangles) : null;
            NavigationBounds = navigationBounds ?? Array.Empty<Bounds>();
        }
        public static Bounds[] NavigationSnapshot()
        {
            var mesh = NavMesh.CalculateTriangulation(); var result = new Bounds[mesh.indices.Length / 3];
            for (int i = 0; i < result.Length; i++)
            {
                var bounds = new Bounds(mesh.vertices[mesh.indices[i * 3]], Vector3.zero);
                bounds.Encapsulate(mesh.vertices[mesh.indices[i * 3 + 1]]);
                bounds.Encapsulate(mesh.vertices[mesh.indices[i * 3 + 2]]);
                bounds.Encapsulate(bounds.max + Vector3.up * 2);
                bounds.Expand(new Vector3(.05f, .05f, .05f)); result[i] = bounds;
            }
            return result;
        }
        public static string NavigationFingerprint()
        {
            var entries = new List<string>();
            foreach (var b in NavigationSnapshot())
            {
                var text = new StringBuilder();
                foreach (float value in new[] { b.center.x,b.center.y,b.center.z,b.size.x,b.size.y,b.size.z })
                    text.Append(value.ToString("R", CultureInfo.InvariantCulture)).Append('|');
                entries.Add(text.ToString());
            }
            entries.Sort(StringComparer.Ordinal);
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(
                Encoding.UTF8.GetBytes(string.Join(";", entries)))).Replace("-", "");
        }
        /// <summary>Geometric importance, not irradiance error. Surface, activity and medium are independent demands.</summary>
        public float Score(Bounds cell, MxFramework.Rendering.MxSceneLightingProbeQuality quality)
        {
            float reach = cell.size.x * quality.ReceiverDistanceFraction;
            int receivers = 0; float variation = 0, minimum = float.PositiveInfinity, maximum = 0, supportRisk = 0; Vector3 firstNormal = Vector3.zero;
            if (quality.AutomaticReceivers && Geometry != null) for (int i = 0; i < 9; i++)
            {
                Vector3 point = cell.center;
                if (i > 0)
                {
                    int corner = i - 1;
                    point += Vector3.Scale(cell.extents * .8f, new Vector3((corner & 1) == 0 ? -1 : 1,
                        (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                }
                if (!Geometry.Nearest(point, reach, out var receiver, out var normal, out float distance)) { maximum=Mathf.Max(maximum,reach); continue; }
                minimum = Mathf.Min(minimum,distance); maximum = Mathf.Max(maximum,distance);
                if (receivers++ == 0) firstNormal = normal;
                else variation = Mathf.Max(variation, 1 - Vector3.Dot(firstNormal, normal));
                if (cell.Contains(receiver)) supportRisk += ReceiverSupportRisk(cell, receiver, normal, quality.MinimumClearance) / 9;
            }
            bool activity = Intersects(cell, quality.ActivityVolumes) || (quality.AutomaticNavigation && Intersects(cell, NavigationBounds));
            float medium = 0;
            if (quality.MediumDemandEnabled)
            {
                float density = quality.MediumDensity * Mathf.Exp(-Mathf.Max(0, cell.center.y - quality.MediumBaseHeight) * quality.MediumHeightFalloff);
                medium = density >= .01f ? Mathf.Min(2, density * 10) : 0;
                if (Intersects(cell, quality.MediumVolumes)) medium = Mathf.Max(medium, 2);
            }
            if (receivers == 0 && !activity && medium == 0) return 0;
            int blocked = 0;
            if (Geometry != null) for (int axis = 0; axis < 3; axis++)
            {
                Vector3 delta = Vector3.zero; delta[axis] = cell.extents[axis] * .95f;
                if (!Geometry.SegmentClear(cell.center - delta, cell.center + delta)) blocked++;
            }
            float clearanceChange = receivers > 1 ? Mathf.Clamp01((maximum-minimum)/cell.size.x) : 0;
            return (receivers > 0 ? 1 : 0) + variation * 4 + clearanceChange * 2 + blocked * 1.5f + (activity ? 3 : 0) + medium + supportRisk * 8;
        }
        // A flat receiver below a roof can have low normal variation while its coarse
        // stencil has only distant exterior support. Reward refinement only when the
        // existing half-spacing lattice actually adds nearby, visible, same-side support.
        // Geometry-only prediction: no camera, captured radiance or authored refinement ROI.
        internal float ReceiverSupportRisk(Bounds cell, Vector3 receiver, Vector3 normal, float clearance)
        {
            float spacing = cell.size.x, offset = Mathf.Max(.08f, clearance * .5f);
            Vector3 start = receiver + normal * offset;
            int coarseSupport = 0; float nearestCoarse = spacing * 2;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 probe = cell.min + new Vector3((corner & 1) == 0 ? 0 : spacing,
                    (corner & 2) == 0 ? 0 : spacing, (corner & 4) == 0 ? 0 : spacing);
                if (!Supports(receiver, normal, start, probe, clearance)) continue;
                coarseSupport++; nearestCoarse = Mathf.Min(nearestCoarse, (probe - receiver).magnitude);
            }
            if (coarseSupport >= 4) return 0;
            float nearestFine = spacing * .625f; bool fineSupport = false;
            for (int z = 0; z < 3; z++) for (int y = 0; y < 3; y++) for (int x = 0; x < 3; x++)
            {
                Vector3 probe = cell.min + new Vector3(x, y, z) * (spacing * .5f);
                float distance = (probe - receiver).magnitude;
                if (distance >= nearestFine || !Supports(receiver, normal, start, probe, clearance)) continue;
                nearestFine = distance; fineSupport = true;
            }
            if (!fineSupport) return 0;
            float gap = Mathf.Clamp01((nearestCoarse - nearestFine) / (spacing * .5f));
            return gap * Mathf.Clamp01((4 - coarseSupport) / 3f);
        }
        private bool Supports(Vector3 receiver, Vector3 normal, Vector3 start, Vector3 probe, float clearance)
        {
            if (Vector3.Dot(normal, probe - receiver) <= .03f) return false;
            if (clearance > 0 && Geometry.Nearest(probe, clearance, out _, out _, out float distance) && distance < clearance) return false;
            return Geometry.SegmentClear(start, probe);
        }
        internal static bool Intersects(Bounds cell, Bounds[] volumes)
        {
            foreach (var volume in volumes ?? Array.Empty<Bounds>()) if (cell.Intersects(volume)) return true;
            return false;
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace MxFramework.Editor.CharacterImport
{
    public static partial class CharacterImportedPackagePrefabBuilder
    {
        // Import-time compatibility for existing baked packages. Match the baker's
        // four interior probes per texel; shared edges are not UV overlap.
        private static int NeutralizeOverlappingHairAo(Color32[] values, int size, Renderer[] renderers)
        {
            int width = checked(size * 2);
            var coverage = new byte[checked(width * width)];
            var meta = new List<Vector2>();
            foreach (Renderer renderer in renderers)
            {
                Mesh mesh = renderer is SkinnedMeshRenderer skin ? skin.sharedMesh
                    : renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null) continue;
                Vector2[] uv = mesh.uv;
                mesh.GetUVs(2, meta);
                if (meta.Count != mesh.vertexCount || uv.Length != mesh.vertexCount)
                    throw new InvalidOperationException("Hair AO overlap requires baked UV0 and HairMeta.");
                int[] indices = mesh.triangles;
                for (int i = 0; i < indices.Length; i += 3)
                {
                    int a = indices[i], b = indices[i + 1], c = indices[i + 2];
                    if (meta[a].y <= .5f || meta[b].y <= .5f || meta[c].y <= .5f) continue;
                    CountHairAoCoverage(coverage, width, uv[a], uv[b], uv[c]);
                }
            }
            int changed = 0;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int start = y * 2 * width + x * 2;
                if (coverage[start] < 2 && coverage[start + 1] < 2 &&
                    coverage[start + width] < 2 && coverage[start + width + 1] < 2) continue;
                int index = y * size + x;
                int profile = values[index].a / 16;
                if (profile != 2 && profile != 3) continue;
                if (values[index].r != 255) changed++;
                values[index].r = 255;
            }
            return changed;
        }

        private static void CountHairAoCoverage(byte[] coverage, int size, Vector2 p0, Vector2 p1, Vector2 p2)
        {
            double x0 = (double)p0.x * size, y0 = (double)p0.y * size;
            double x1 = (double)p1.x * size, y1 = (double)p1.y * size;
            double x2 = (double)p2.x * size, y2 = (double)p2.y * size;
            if (double.IsNaN(x0 + y0 + x1 + y1 + x2 + y2) ||
                double.IsInfinity(x0 + y0 + x1 + y1 + x2 + y2))
                throw new InvalidOperationException("Non-finite hair AO UV.");
            double ax = x1 - x0, ay = y1 - y0, bx = x2 - x0, by = y2 - y0;
            double determinant = ax * by - ay * bx;
            if (Math.Abs(determinant) < 1e-12) return;
            int lowX = (int)Math.Max(0, Math.Min(size, Math.Floor(Math.Min(x0, Math.Min(x1, x2)))));
            int lowY = (int)Math.Max(0, Math.Min(size, Math.Floor(Math.Min(y0, Math.Min(y1, y2)))));
            int highX = (int)Math.Min(size - 1, Math.Max(-1, Math.Ceiling(Math.Max(x0, Math.Max(x1, x2)))));
            int highY = (int)Math.Min(size - 1, Math.Max(-1, Math.Ceiling(Math.Max(y0, Math.Max(y1, y2)))));
            for (int y = lowY; y <= highY; y++)
            for (int x = lowX; x <= highX; x++)
            {
                double dx = x + .5 - x0, dy = y + .5 - y0;
                double u = (dx * by - dy * bx) / determinant;
                double v = (ax * dy - ay * dx) / determinant;
                if (u <= 1e-7 || v <= 1e-7 || u + v >= 1 - 1e-7) continue;
                int index = y * size + x;
                if (coverage[index] < 2) coverage[index]++;
            }
        }
    }
}

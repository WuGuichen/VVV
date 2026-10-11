using System;
using System.Collections.Generic;

namespace MxFramework.Game.CharacterTest.Editor
{
    // Plain authoring math: deterministic, testable without Unity or native assets.
    [Serializable] internal sealed class MeadowRecipe
    {
        public string format;
        public int seed;
        public float centerX, centerZ;
        public float width, depth, spacing, clusterSize, density, flowerCoverage, seedHeadCoverage;
        // Zero targetCount keeps the original small-sample distribution contract.
        public int targetCount;
        public float edgeFade, drawDistance;
        public MeadowLandscape landscape;
        public MeadowPlant[] types;
    }
    [Serializable] internal sealed class MeadowPlant
    {
        public string id, name, kind;
        public int seed, leaves;
        public float height, width, lean;
        public MeadowColor root, tip;
        public MeadowPalette palette;
    }
    [Serializable] internal sealed class MeadowPalette
    {
        public MeadowColor rootMin, rootMax, tipMin, tipMax;
    }
    [Serializable] internal sealed class MeadowRange { public float min, max; }
    [Serializable] internal sealed class MeadowControlPoint { public float x, z; }
    [Serializable] internal sealed class MeadowStyle
    {
        public float density;
        // Weights always refer to the existing recipe's four types, in order.
        public float[] weights;
        // Optional region override; base style requires four nominal meter ranges.
        public MeadowRange[] heights;
    }
    [Serializable] internal sealed class MeadowRegion
    {
        public string id, shape;
        public int seed;
        public MeadowControlPoint center;
        public float radiusX, radiusZ, rotation, halfWidth, transition, edgeNoise;
        public MeadowControlPoint[] points;
        public MeadowStyle style;
    }
    [Serializable] internal sealed class MeadowLandscape
    {
        public MeadowStyle baseStyle, edgeStyle;
        public MeadowRange widthScale;
        public float macroSize, detailSize, densityNoiseMin, densityNoiseMax, yaw, yawSpread;
        public MeadowRegion[] regions;
    }
    internal sealed class MeadowFieldSample
    {
        internal float Density;
        internal readonly float[] Weights = new float[4], MinHeight = new float[4], MaxHeight = new float[4];
    }
    [Serializable] internal struct MeadowColor
    {
        public float r, g, b;
        internal MeadowColor(float r, float g, float b) { this.r = r; this.g = g; this.b = b; }
        internal static MeadowColor Lerp(MeadowColor a, MeadowColor b, float t) =>
            new MeadowColor(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t);
    }
    internal readonly struct MeadowPoint
    {
        internal readonly float X, Y, Z;
        internal MeadowPoint(float x, float y, float z) { X = x; Y = y; Z = z; }
        internal static MeadowPoint Add(MeadowPoint a, MeadowPoint b) => new MeadowPoint(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    }
    internal sealed class MeadowMeshData
    {
        internal readonly List<MeadowPoint> Points = new List<MeadowPoint>();
        internal readonly List<MeadowColor> Colors = new List<MeadowColor>();
        internal readonly List<float> Heights = new List<float>();
        internal readonly List<float[]> MaskUvs = new List<float[]>();
        internal readonly List<int> Indices = new List<int>();
        internal float PlantHeight;
        internal int Add(MeadowPoint p, MeadowColor c, float maskU = .25f, float maskV = .5f)
        { int i = Points.Count; Points.Add(p); Colors.Add(c); Heights.Add(p.Y / PlantHeight); MaskUvs.Add(new[] { maskU, maskV }); return i; }
        internal void Tri(int a, int b, int c) { Indices.Add(a); Indices.Add(b); Indices.Add(c); }
    }
    internal readonly struct MeadowPlacement
    {
        internal readonly int Type, Seed;
        internal readonly float X, Z, Yaw, Scale, HeightScale;
        internal MeadowPlacement(int type, int seed, float x, float z, float yaw, float scale)
        { Type = type; Seed = seed; X = x; Z = z; Yaw = yaw; Scale = scale; HeightScale = scale; }
        internal MeadowPlacement(int type, int seed, float x, float z, float yaw, float scale, float heightScale)
        { Type = type; Seed = seed; X = x; Z = z; Yaw = yaw; Scale = scale; HeightScale = heightScale; }
    }
    internal static class MeadowGeometry
    {
        internal static void Validate(MeadowRecipe r)
        {
            if (r == null || (r.format != "wgame.meadow-sample.v1" && r.format != "wgame.meadow-sample.v2") ||
                r.types == null || r.types.Length != 4)
                throw new ArgumentException("Expected the four-type meadow recipe.");
            if (!In(r.centerX, -10000, 10000) || !In(r.centerZ, -10000, 10000) ||
                !In(r.width, 1, 256) || !In(r.depth, 1, 256) || !In(r.spacing, .15f, 2) ||
                (r.landscape == null && !In(r.clusterSize, 1, 12)) || !In(r.density, 0, 1) || !In(r.flowerCoverage, 0, .3f) ||
                !In(r.seedHeadCoverage, 0, .3f)) throw new ArgumentException("Invalid meadow distribution.");
            if (r.targetCount < 0 || r.targetCount > 200000 ||
                (r.targetCount > 0 && (!In(r.edgeFade, 1, 24) || !In(r.drawDistance, 50, 500))))
                throw new ArgumentException("Invalid large-meadow count, border or viewing range.");
            var ids = new HashSet<string>();
            foreach (var p in r.types)
            {
                if (p == null || string.IsNullOrEmpty(p.id) || !ids.Add(p.id) ||
                    (p.kind != "grass" && p.kind != "flower" && p.kind != "plume") ||
                    !In(p.height, .1f, 2) || !In(p.width, .01f, .1f) || !In(p.lean, .01f, .4f) ||
                    p.leaves < 4 || p.leaves > 20 || !ValidColor(p.root) || !ValidColor(p.tip))
                    throw new ArgumentException("Invalid meadow plant.");
                if (p.palette != null && (!ValidColor(p.palette.rootMin) || !ValidColor(p.palette.rootMax) ||
                    !ValidColor(p.palette.tipMin) || !ValidColor(p.palette.tipMax)))
                    throw new ArgumentException("Invalid meadow palette.");
            }
            if (r.format == "wgame.meadow-sample.v2" && r.landscape == null)
                throw new ArgumentException("v2 requires the editable landscape.");
            if (r.landscape != null) ValidateLandscape(r);
        }
        private static bool In(float x, float a, float b) => !float.IsNaN(x) && !float.IsInfinity(x) && x >= a && x <= b;
        private static bool ValidColor(MeadowColor c) => In(c.r, 0, 1) && In(c.g, 0, 1) && In(c.b, 0, 1);
        internal static MeadowMeshData Build(MeadowPlant p, int lod)
        {
            if (lod < 0 || lod > 2) throw new ArgumentOutOfRangeException(nameof(lod));
            var m = new MeadowMeshData { PlantHeight = p.height };
            if (p.kind == "grass") Grass(m, p, lod);
            else if (p.kind == "flower") Flower(m, p, lod);
            else Plume(m, p, lod);
            return m;
        }
        private static void Grass(MeadowMeshData m, MeadowPlant p, int lod)
        {
            // The retained leaves are a nested, angularly balanced subset of the
            // same authored centerlines, not regenerated random shapes per LOD.
            for (int i = 0; i < p.leaves; i++)
            {
                if (lod == 1 && i % 2 != 0) continue;
                if (lod == 2 && i % 4 != 0) continue;
                float h = p.height * (.62f + .38f * Hash(p.seed, i, 1));
                float yaw = i * 2.399963f + .23f * (Hash(p.seed, i, 2) - .5f);
                float width = p.width * (.75f + .35f * Hash(p.seed, i, 3));
                float lean = p.lean * (.7f + .6f * Hash(p.seed, i, 4));
                var root = new MeadowPoint(Cos(yaw) * .025f, 0, Sin(yaw) * .025f);
                Blade(m, root, h, width, lean, yaw, lod, RootColor(p, i), TipColor(p, i));
            }
        }
        private static void Blade(MeadowMeshData m, MeadowPoint root, float height, float width,
            float lean, float yaw, int lod, MeadowColor bottom, MeadowColor top)
        {
            int sections = lod == 0 ? 3 : 2, columns = lod == 2 ? 2 : 3;
            int start = m.Points.Count;
            for (int row = 0; row <= sections; row++)
            {
                float t = row == sections ? 1 : row / (float)sections;
                float taper = row == 0 ? .28f : row == sections ? 0 : row == 1 ? 1 : .58f;
                var center = MeadowPoint.Add(root, new MeadowPoint(Sin(yaw) * lean * t * t,
                    height * t, Cos(yaw) * lean * t * t));
                int n = row == sections ? 1 : columns;
                for (int col = 0; col < n; col++)
                {
                    float side = row == sections ? 0 : columns == 2 ? col * 2 - 1 : col - 1;
                    float offset = width * taper * .5f * side;
                    float fold = columns == 3 && col == 1 && row != sections ? width * .2f * Sin(t * (float)Math.PI) : 0;
                    m.Add(MeadowPoint.Add(center, new MeadowPoint(Cos(yaw) * offset + Sin(yaw) * fold,
                        0, -Sin(yaw) * offset + Cos(yaw) * fold)), MeadowColor.Lerp(bottom, top, t));
                }
            }
            for (int row = 0; row < sections - 1; row++)
                for (int c = 0; c < columns - 1; c++)
                { int a = start + row * columns + c; m.Tri(a, a + columns, a + 1); m.Tri(a + 1, a + columns, a + columns + 1); }
            int last = start + (sections - 1) * columns, tip = start + sections * columns;
            for (int c = 0; c < columns - 1; c++) m.Tri(last + c, tip, last + c + 1);
        }
        private static MeadowPoint StemCenter(float height, float lean, float yaw, float t) =>
            new MeadowPoint(Sin(yaw) * lean * t * t, height * t, Cos(yaw) * lean * t * t);
        private static void Stem(MeadowMeshData m, MeadowPoint root, float h, float lean, float yaw,
            int lod, MeadowColor color)
        {
            // Narrow ribbons have coherent UV height, so stems and their heads
            // receive exactly the same shared world-space wind at an attachment.
            for (int plane = 0; plane < (lod == 0 ? 2 : 1); plane++)
            {
                float sideYaw = yaw + plane * (float)Math.PI * .5f;
                int start = m.Points.Count, sections = lod == 2 ? 1 : 2;
                for (int row = 0; row <= sections; row++)
                {
                    var center = MeadowPoint.Add(root, StemCenter(h, lean, yaw, row / (float)sections));
                    for (int c = 0; c < 2; c++) m.Add(MeadowPoint.Add(center, new MeadowPoint(
                        Cos(sideYaw) * .006f * (c * 2 - 1), 0, -Sin(sideYaw) * .006f * (c * 2 - 1))), color);
                }
                for (int row = 0; row < sections; row++)
                { int a = start + row * 2; m.Tri(a, a + 2, a + 1); m.Tri(a + 1, a + 2, a + 3); }
            }
        }
        private static void Flower(MeadowMeshData m, MeadowPlant p, int lod)
        {
            int heads = lod == 2 ? 2 : 3, petals = lod == 0 ? 6 : 4;
            for (int i = 0; i < heads; i++)
            {
                var rootColor = RootColor(p, i); var tipColor = TipColor(p, i);
                float yaw = i * 2.4f, h = p.height * (i == 0 ? 1 : i == 1 ? .78f : .9f);
                var root = new MeadowPoint(Sin(yaw) * .04f, 0, Cos(yaw) * .04f);
                Stem(m, root, h, p.lean, yaw, lod, rootColor);
                if (lod != 2)
                {
                    Blade(m, MeadowPoint.Add(root, StemCenter(h, p.lean, yaw, .3f)), h * .16f,
                        p.width, .07f, yaw + 1, 2, rootColor, new MeadowColor(.34f, .48f, .15f));
                    if (lod == 0) Blade(m, MeadowPoint.Add(root, StemCenter(h, p.lean, yaw, .5f)), h * .12f,
                        p.width * .7f, .055f, yaw - 1, 2, rootColor, new MeadowColor(.34f, .48f, .15f));
                }
                var center = MeadowPoint.Add(root, StemCenter(h, p.lean, yaw, 1));
                for (int petal = 0; petal < petals; petal++)
                {
                    float a = yaw + petal * (float)Math.PI * 2 / petals, radius = .068f;
                    int r = m.Add(center, MeadowColor.Lerp(tipColor, rootColor, .1f));
                    int left = m.Add(MeadowPoint.Add(center, new MeadowPoint(Cos(a - .4f) * radius * .7f,
                        -.010f, Sin(a - .4f) * radius * .7f)), tipColor);
                    int tip = m.Add(MeadowPoint.Add(center, new MeadowPoint(Cos(a) * radius,
                        -.025f, Sin(a) * radius)), tipColor);
                    int right = m.Add(MeadowPoint.Add(center, new MeadowPoint(Cos(a + .4f) * radius * .7f,
                        -.010f, Sin(a + .4f) * radius * .7f)), tipColor);
                    m.Tri(r, left, tip); m.Tri(r, tip, right);
                    var gold = new MeadowColor(.88f, .57f, .12f);
                    int c = m.Add(MeadowPoint.Add(center, new MeadowPoint(0, .006f, 0)), gold);
                    int d = m.Add(MeadowPoint.Add(center, new MeadowPoint(Cos(a) * .017f, .007f, Sin(a) * .017f)), gold);
                    int e = m.Add(MeadowPoint.Add(center, new MeadowPoint(Cos(a + (float)Math.PI * 2 / petals) * .017f,
                        .007f, Sin(a + (float)Math.PI * 2 / petals) * .017f)), gold);
                    m.Tri(c, d, e);
                }
            }
        }
        private static void Plume(MeadowMeshData m, MeadowPlant p, int lod)
        {
            int leaves = lod == 0 ? 6 : lod == 1 ? 4 : 2;
            for (int i = 0; i < leaves; i++) Blade(m, new MeadowPoint(0, 0, 0), p.height * .4f,
                p.width, .25f, i * 2.399963f, lod, RootColor(p, i), MeadowColor.Lerp(RootColor(p, i), TipColor(p, i), .32f));
            for (int stalk = 0; stalk < 2; stalk++)
            {
                var rootColor = RootColor(p, stalk); var tipColor = TipColor(p, stalk);
                float yaw = stalk * 2.8f + .4f, h = p.height * (stalk == 0 ? 1 : .85f);
                var root = new MeadowPoint(stalk == 0 ? -.025f : .025f, 0, 0);
                Stem(m, root, h, p.lean, yaw, lod, MeadowColor.Lerp(rootColor, tipColor, .45f));
                int planes = lod == 0 ? 3 : 2, sections = lod == 0 ? 3 : lod == 1 ? 2 : 1;
                for (int plane = 0; plane < planes; plane++)
                {
                    float a = yaw + plane * (float)Math.PI / planes;
                    int start = m.Points.Count;
                    for (int row = 0; row <= sections; row++)
                    {
                        float v = row / (float)sections, t = .67f + .36f * v;
                        var center = MeadowPoint.Add(root, StemCenter(h, p.lean, yaw, t));
                        var color = MeadowColor.Lerp(MeadowColor.Lerp(rootColor, tipColor, .75f), tipColor, v);
                        for (int side = 0; side < 2; side++)
                            m.Add(MeadowPoint.Add(center, new MeadowPoint(Cos(a) * (p.width * 2.7f) * (side * 2 - 1),
                                0, Sin(a) * (p.width * 2.7f) * (side * 2 - 1))), color, .505f + .49f * side, v);
                    }
                    for (int row = 0; row < sections; row++)
                    { int a0 = start + row * 2; m.Tri(a0, a0 + 2, a0 + 1); m.Tri(a0 + 1, a0 + 2, a0 + 3); }
                }
            }
        }
        internal static List<MeadowPlacement> Scatter(MeadowRecipe r)
        {
            if (r != null && r.landscape != null) return ScatterLandscape(r);
            if (r != null && r.targetCount > 0) return ScatterLarge(r);
            Validate(r); var result = new List<MeadowPlacement>();
            int nx = (int)Math.Ceiling(r.width / r.spacing), nz = (int)Math.Ceiling(r.depth / r.spacing);
            for (int z = 0; z < nz; z++) for (int x = 0; x < nx; x++)
            {
                float px = -r.width * .5f + (x + .12f + .76f * Hash(r.seed, x, z)) * r.spacing;
                float pz = -r.depth * .5f + (z + .12f + .76f * Hash(r.seed + 1, x, z)) * r.spacing;
                int cx = (int)Math.Floor(px / r.clusterSize), cz = (int)Math.Floor(pz / r.clusterSize);
                float best = float.MaxValue; int clusterX = 0, clusterZ = 0;
                for (int dz = -1; dz <= 1; dz++) for (int dx = -1; dx <= 1; dx++)
                {
                    int gx = cx + dx, gz = cz + dz;
                    float ax = (gx + Hash(r.seed + 5, gx, gz)) * r.clusterSize;
                    float az = (gz + Hash(r.seed + 7, gx, gz)) * r.clusterSize;
                    float d = (px - ax) * (px - ax) + (pz - az) * (pz - az);
                    if (d < best) { best = d; clusterX = gx; clusterZ = gz; }
                }
                float patch = Hash(r.seed + 13, clusterX, clusterZ);
                float edge = (float)Math.Sqrt(best) / r.clusterSize;
                float coverage = r.density * (.48f + .52f * patch) * Math.Max(.15f, 1 - edge * .75f);
                // A winding sparse channel and feathered border keep authored
                // patches readable, rather than making a solid rectangular carpet.
                float channel = Math.Abs(pz - Sin(px * .32f) * 1.25f);
                if (channel < .45f) coverage *= .13f;
                float border = Math.Min(r.width * .5f - Math.Abs(px), r.depth * .5f - Math.Abs(pz));
                coverage *= Math.Min(1, Math.Max(0, border / .8f));
                if (Hash(r.seed + 19, x, z) > coverage) continue;
                float accent = Hash(r.seed + 23, clusterX, clusterZ), roll = Hash(r.seed + 29, x, z);
                int type = patch > .4f ? 1 : 0;
                if (accent < r.flowerCoverage * 2 && roll < .38f) type = 2;
                else if (accent > .65f && roll < r.seedHeadCoverage * 1.7f) type = 3;
                float yaw = Hash(r.seed + 31, clusterX, clusterZ) * 360 + (Hash(r.seed + 37, x, z) - .5f) * 38;
                float scale = .8f + patch * .25f + (Hash(r.seed + 41, x, z) - .5f) * .16f;
                int seed = unchecked((int)(HashBits(r.seed, x, z) & 0x7fffffff));
                result.Add(new MeadowPlacement(type, seed, px, pz, yaw, scale));
            }
            return result;
        }
        private static List<MeadowPlacement> ScatterLarge(MeadowRecipe r)
        {
            Validate(r);
            var result = new List<MeadowPlacement>();
            int nx = (int)Math.Ceiling(r.width / r.spacing), nz = (int)Math.Ceiling(r.depth / r.spacing);
            for (int z = 0; z < nz; z++) for (int x = 0; x < nx; x++)
            {
                // At least .4 * spacing between adjacent cells (.16m by default),
                // exceeding the existing .12m authoring rejection radius.
                float px = -r.width * .5f + (x + .2f + .6f * Hash(r.seed, x, z)) * r.spacing;
                float pz = -r.depth * .5f + (z + .2f + .6f * Hash(r.seed + 1, x, z)) * r.spacing;
                if (px > r.width * .5f || pz > r.depth * .5f) continue;
                float border = Math.Min(r.width * .5f - Math.Abs(px), r.depth * .5f - Math.Abs(pz));
                float edge = Smooth(border / r.edgeFade);
                float broad = Noise(r.seed + 11, px / 19f, pz / 19f);
                float clump = Noise(r.seed + 17, px / r.clusterSize, pz / r.clusterSize);
                float coverage = r.density * (.52f + .32f * broad + .16f * clump) * edge;
                // A broad meandering low-grass corridor with feathered shoulders,
                // plus open pockets; these affect occupancy, not just type tint.
                float pathCenter = Sin(px * .047f) * 8f + Sin(px * .019f + 1.2f) * 3f - 7f;
                float path = Math.Abs(pz - pathCenter);
                coverage *= .10f + .90f * Smooth((path - 1.1f) / 3.5f);
                float clearing = Math.Max(Patch(px, pz, -37, 16, 12, 9),
                    Patch(px, pz, 34, -26, 15, 10));
                coverage *= 1 - .78f * clearing;
                if (Hash(r.seed + 19, x, z) >= coverage) continue;

                float flowers = Math.Max(Patch(px, pz, -30, -30, 21, 12),
                    Math.Max(Patch(px, pz, 18, 23, 25, 14), Patch(px, pz, 54, -24, 16, 13)));
                // A long, irregular plume bank, with gaps and mixed grass at its
                // margins instead of isolated uniformly sprinkled stalks.
                float plumeCenter = 16f - px * .14f + Sin(px * .057f) * 5f;
                float plumes = Smooth(1 - Math.Abs(pz - plumeCenter) / 11f) *
                    (.45f + .55f * Noise(r.seed + 53, px / 12f, pz / 12f));
                float roll = Hash(r.seed + 29, x, z);
                int type = Hash(r.seed + 27, x, z) < .40f + .65f * edge * broad ? 1 : 0;
                if (roll < flowers * r.flowerCoverage * 4.5f) type = 2;
                else if (roll > 1 - plumes * r.seedHeadCoverage * 4.4f) type = 3;
                if (type < 2 && (border < r.edgeFade * .65f || path < 3)) type = 0;
                // Smooth correlated orientation keeps large clumps legible;
                // stable per-cell variation supplies individual scale/phase.
                float yaw = 35 + Noise(r.seed + 31, px / r.clusterSize, pz / r.clusterSize) * 125 +
                    (Hash(r.seed + 37, x, z) - .5f) * 46;
                float scale = .82f + clump * .25f + (Hash(r.seed + 41, x, z) - .5f) * .14f;
                int seed = unchecked((int)(HashBits(r.seed, x, z) & 0x7fffffff));
                result.Add(new MeadowPlacement(type, seed, px, pz, yaw, scale));
            }
            if (result.Count < r.targetCount)
                throw new InvalidOperationException("Meadow coverage generated " + result.Count +
                    " records, below target " + r.targetCount + ". Increase area or density; do not silently underfill.");
            // Uniform deterministic thinning preserves field proportions and
            // sparse borders. Tie-breakers give a total order across runtimes.
            result.Sort((a, b) =>
            {
                uint ha = HashBits(r.seed + 101, a.Seed, 0), hb = HashBits(r.seed + 101, b.Seed, 0);
                int c = ha.CompareTo(hb); if (c != 0) return c;
                c = a.Z.CompareTo(b.Z); return c != 0 ? c : a.X.CompareTo(b.X);
            });
            result.RemoveRange(r.targetCount, result.Count - r.targetCount);
            result.Sort((a, b) => { int c = a.Z.CompareTo(b.Z); return c != 0 ? c : a.X.CompareTo(b.X); });
            return result;
        }
        private static MeadowColor RootColor(MeadowPlant p, int index) => p.palette == null ? p.root :
            MeadowColor.Lerp(p.palette.rootMin, p.palette.rootMax, Hash(p.seed + 211, index, 0));
        private static MeadowColor TipColor(MeadowPlant p, int index) => p.palette == null ? p.tip :
            MeadowColor.Lerp(p.palette.tipMin, p.palette.tipMax, Hash(p.seed + 223, index, 0));
        private static void ValidateLandscape(MeadowRecipe r)
        {
            var l = r.landscape;
            if (r.targetCount < 40000 || r.targetCount > 60000 ||
                !In(l.macroSize, 4, 80) || !In(l.detailSize, 1, 24) ||
                !In(l.densityNoiseMin, .1f, 1) || !In(l.densityNoiseMax, l.densityNoiseMin, 1) ||
                !In(l.yaw, -360, 360) || !In(l.yawSpread, 0, 180) || l.widthScale == null ||
                !In(l.widthScale.min, .6f, 1.4f) || !In(l.widthScale.max, l.widthScale.min, 1.4f) ||
                l.regions == null || l.regions.Length > 32)
                throw new ArgumentException("Invalid editable landscape: keep the authored target near 50k.");
            ValidateStyle(l.baseStyle, r, true); ValidateStyle(l.edgeStyle, r, false);
            var ids = new HashSet<string>();
            foreach (var region in l.regions)
            {
                if (region == null || string.IsNullOrWhiteSpace(region.id) || !ids.Add(region.id) ||
                    !In(region.transition, .1f, 24) || !In(region.edgeNoise, 0, 8))
                    throw new ArgumentException("Invalid region ID or boundary transition.");
                ValidateStyle(region.style, r, false);
                if (region.shape == "ellipse")
                {
                    if (!ValidPoint(region.center) || !In(region.radiusX, 1, 160) ||
                        !In(region.radiusZ, 1, 160) || !In(region.rotation, -360, 360))
                        throw new ArgumentException("Invalid ellipse: " + region.id);
                }
                else if (region.shape == "ribbon" || region.shape == "polygon")
                {
                    int minimum = region.shape == "polygon" ? 3 : 2;
                    if (region.points == null || region.points.Length < minimum || region.points.Length > 32 ||
                        (region.shape == "ribbon" && !In(region.halfWidth, .2f, 32)))
                        throw new ArgumentException("Invalid control points: " + region.id);
                    for (int i = 0; i < region.points.Length; i++)
                    {
                        if (!ValidPoint(region.points[i])) throw new ArgumentException("Invalid region point.");
                        if (i > 0 && DistanceSquared(region.points[i], region.points[i - 1]) < .01f)
                            throw new ArgumentException("Repeated adjacent control point: " + region.id);
                    }
                    if (region.shape == "polygon") ValidatePolygon(region);
                }
                else throw new ArgumentException("Expected ellipse, ribbon or polygon: " + region.id);
            }
        }
        private static bool ValidPoint(MeadowControlPoint p) => p != null && In(p.x, -256, 256) && In(p.z, -256, 256);
        private static float DistanceSquared(MeadowControlPoint a, MeadowControlPoint b) =>
            (a.x - b.x) * (a.x - b.x) + (a.z - b.z) * (a.z - b.z);
        private static void ValidateStyle(MeadowStyle s, MeadowRecipe r, bool requiredHeights)
        {
            if (s == null || !In(s.density, 0, 1) || s.weights == null || s.weights.Length != 4)
                throw new ArgumentException("Each style requires density and four type weights.");
            float total = 0;
            foreach (float w in s.weights) { if (!In(w, 0, 100)) throw new ArgumentException("Invalid type weight."); total += w; }
            if (total <= 0) throw new ArgumentException("Type weights must have a positive sum.");
            if (s.heights == null) { if (requiredHeights) throw new ArgumentException("Base style requires four height ranges."); return; }
            if (s.heights.Length != 4) throw new ArgumentException("Expected four nominal height ranges.");
            for (int i = 0; i < 4; i++)
            {
                var h = s.heights[i];
                if (h == null || !In(h.min, r.types[i].height * .5f, r.types[i].height * 1.6f) ||
                    !In(h.max, h.min, r.types[i].height * 1.6f))
                    throw new ArgumentException("Height range must fit the existing type's authored size.");
            }
        }
        private static float Cross(MeadowControlPoint a, MeadowControlPoint b, MeadowControlPoint c) =>
            (b.x - a.x) * (c.z - a.z) - (b.z - a.z) * (c.x - a.x);
        private static bool Intersects(MeadowControlPoint a, MeadowControlPoint b, MeadowControlPoint c, MeadowControlPoint d)
        {
            if (Math.Max(a.x, b.x) < Math.Min(c.x, d.x) || Math.Max(c.x, d.x) < Math.Min(a.x, b.x) ||
                Math.Max(a.z, b.z) < Math.Min(c.z, d.z) || Math.Max(c.z, d.z) < Math.Min(a.z, b.z)) return false;
            return Cross(a, b, c) * Cross(a, b, d) <= 0 && Cross(c, d, a) * Cross(c, d, b) <= 0;
        }
        private static void ValidatePolygon(MeadowRegion r)
        {
            float area = 0; int n = r.points.Length;
            for (int i = 0; i < n; i++)
            {
                var a = r.points[i]; var b = r.points[(i + 1) % n];
                if (DistanceSquared(a, b) < .01f) throw new ArgumentException("Repeated polygon point.");
                area += a.x * b.z - b.x * a.z;
                for (int j = i + 1; j < n; j++)
                {
                    if (j == i + 1 || (i == 0 && j == n - 1)) continue;
                    if (Intersects(a, b, r.points[j], r.points[(j + 1) % n]))
                        throw new ArgumentException("Self-intersecting polygon: " + r.id);
                }
            }
            if (Math.Abs(area) < .01f) throw new ArgumentException("Polygon has no area: " + r.id);
        }
        private static float SegmentDistance(float x, float z, MeadowControlPoint a, MeadowControlPoint b)
        {
            float dx = b.x - a.x, dz = b.z - a.z;
            float t = Math.Max(0, Math.Min(1, ((x - a.x) * dx + (z - a.z) * dz) / (dx * dx + dz * dz)));
            dx = x - a.x - t * dx; dz = z - a.z - t * dz;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }
        internal static float RegionMask(MeadowRegion r, float x, float z)
        {
            float signed;
            if (r.shape == "ellipse")
            {
                float angle = r.rotation * (float)Math.PI / 180, dx = x - r.center.x, dz = z - r.center.z;
                float u = (dx * Cos(angle) + dz * Sin(angle)) / r.radiusX;
                float v = (-dx * Sin(angle) + dz * Cos(angle)) / r.radiusZ;
                signed = (1 - (float)Math.Sqrt(u * u + v * v)) * Math.Min(r.radiusX, r.radiusZ);
            }
            else
            {
                float distance = float.MaxValue; bool inside = false;
                int n = r.points.Length, edges = r.shape == "polygon" ? n : n - 1;
                for (int i = 0; i < edges; i++)
                {
                    var a = r.points[i]; var b = r.points[(i + 1) % n];
                    distance = Math.Min(distance, SegmentDistance(x, z, a, b));
                    if (r.shape == "polygon" && (a.z > z) != (b.z > z) &&
                        x < a.x + (z - a.z) * (b.x - a.x) / (b.z - a.z)) inside = !inside;
                }
                signed = r.shape == "ribbon" ? r.halfWidth - distance : inside ? distance : -distance;
            }
            signed += (Noise(r.seed, x / 8f, z / 8f) - .5f) * r.edgeNoise;
            return Smooth(.5f + signed / (2 * r.transition));
        }
        private static void BlendStyle(MeadowFieldSample s, MeadowStyle style, float influence)
        {
            float sum = 0; foreach (float w in style.weights) sum += w;
            s.Density += (style.density - s.Density) * influence;
            for (int i = 0; i < 4; i++)
            {
                s.Weights[i] += (style.weights[i] / sum - s.Weights[i]) * influence;
                if (style.heights == null) continue;
                s.MinHeight[i] += (style.heights[i].min - s.MinHeight[i]) * influence;
                s.MaxHeight[i] += (style.heights[i].max - s.MaxHeight[i]) * influence;
            }
        }
        internal static MeadowFieldSample SampleLandscape(MeadowRecipe r, float x, float z)
        {
            var l = r.landscape; var s = new MeadowFieldSample();
            BlendStyle(s, l.baseStyle, 1);
            foreach (var region in l.regions) BlendStyle(s, region.style, RegionMask(region, x, z));
            float border = Math.Min(r.width * .5f - Math.Abs(x), r.depth * .5f - Math.Abs(z));
            float edge = Smooth(border / r.edgeFade);
            BlendStyle(s, l.edgeStyle, 1 - edge);
            float noise = .75f * Noise(r.seed + 11, x / l.macroSize, z / l.macroSize) +
                .25f * Noise(r.seed + 17, x / l.detailSize, z / l.detailSize);
            s.Density *= r.density * (l.densityNoiseMin + (l.densityNoiseMax - l.densityNoiseMin) * noise) * edge;
            return s;
        }
        private static List<MeadowPlacement> ScatterLandscape(MeadowRecipe r)
        {
            Validate(r); var l = r.landscape; var result = new List<MeadowPlacement>();
            int nx = (int)Math.Ceiling(r.width / r.spacing), nz = (int)Math.Ceiling(r.depth / r.spacing);
            for (int z = 0; z < nz; z++) for (int x = 0; x < nx; x++)
            {
                float px = -r.width * .5f + (x + .2f + .6f * Hash(r.seed, x, z)) * r.spacing;
                float pz = -r.depth * .5f + (z + .2f + .6f * Hash(r.seed + 1, x, z)) * r.spacing;
                if (px > r.width * .5f || pz > r.depth * .5f) continue;
                var sample = SampleLandscape(r, px, pz);
                if (Hash(r.seed + 19, x, z) >= sample.Density) continue;
                float roll = Hash(r.seed + 29, x, z), sum = sample.Weights[0]; int type = 0;
                while (type < 3 && roll >= sum) sum += sample.Weights[++type];
                float height = sample.MinHeight[type] + (sample.MaxHeight[type] - sample.MinHeight[type]) *
                    (.65f * Noise(r.seed + 43, px / l.detailSize, pz / l.detailSize) + .35f * Hash(r.seed + 41, x, z));
                float scale = l.widthScale.min + (l.widthScale.max - l.widthScale.min) * Hash(r.seed + 47, x, z);
                float yaw = l.yaw + (Noise(r.seed + 31, px / l.detailSize, pz / l.detailSize) - .5f) * l.yawSpread +
                    (Hash(r.seed + 37, x, z) - .5f) * l.yawSpread * .35f;
                int seed = unchecked((int)(HashBits(r.seed, x, z) & 0x7fffffff));
                result.Add(new MeadowPlacement(type, seed, px, pz, yaw, scale, height / r.types[type].height));
            }
            if (result.Count < r.targetCount)
                throw new InvalidOperationException("Editable landscape generated " + result.Count +
                    " candidates, below target " + r.targetCount + ". Adjust density or region footprint.");
            result.Sort((a, b) =>
            {
                int c = HashBits(r.seed + 101, a.Seed, 0).CompareTo(HashBits(r.seed + 101, b.Seed, 0));
                if (c != 0) return c;
                c = a.Z.CompareTo(b.Z); return c != 0 ? c : a.X.CompareTo(b.X);
            });
            result.RemoveRange(r.targetCount, result.Count - r.targetCount);
            result.Sort((a, b) => { int c = a.Z.CompareTo(b.Z); return c != 0 ? c : a.X.CompareTo(b.X); });
            return result;
        }
        private static float Smooth(float t) { t = Math.Max(0, Math.Min(1, t)); return t * t * (3 - 2 * t); }
        private static float Patch(float x, float z, float cx, float cz, float rx, float rz)
        {
            float dx = (x - cx) / rx, dz = (z - cz) / rz;
            return Smooth(1 - (float)Math.Sqrt(dx * dx + dz * dz));
        }
        private static float Noise(int seed, float x, float z)
        {
            int ix = (int)Math.Floor(x), iz = (int)Math.Floor(z);
            float tx = Smooth(x - ix), tz = Smooth(z - iz);
            float a = Hash(seed, ix, iz), b = Hash(seed, ix + 1, iz);
            float c = Hash(seed, ix, iz + 1), d = Hash(seed, ix + 1, iz + 1);
            return (a + (b - a) * tx) * (1 - tz) + (c + (d - c) * tx) * tz;
        }
        private static float Sin(float x) => (float)Math.Sin(x);
        // Left atlas half is fully covered for geometric grass/flowers/stems.
        // Right half is an original feather envelope with directional filaments.
        internal static float Coverage(float u, float v)
        {
            if (u < .5f) return 1;
            float x = (u - .5f) * 2 - .5f;
            float envelope = .44f * (float)Math.Pow(Math.Max(0, Sin((float)Math.PI * v)), .85);
            float phase = v * 143f + Math.Abs(x) * 46f;
            float fringe = .017f * Sin(phase) + .011f * Sin(phase * 2.37f);
            float edge = Math.Max(0, Math.Min(1, (envelope + fringe - Math.Abs(x)) * 95f));
            float fiber = Math.Abs(x) < .07f ? 1 : .28f + .72f * (.5f + .5f * Sin(v * 197f + Math.Abs(x) * 79f));
            return edge * fiber;
        }
        private static float Cos(float x) => (float)Math.Cos(x);
        private static uint HashBits(int seed, int x, int z)
        {
            unchecked { uint h = (uint)seed ^ (uint)x * 0x9e3779b9u ^ (uint)z * 0x85ebca6bu;
                h ^= h >> 16; h *= 0x7feb352du; h ^= h >> 15; h *= 0x846ca68bu; return h ^ (h >> 16); }
        }
        private static float Hash(int seed, int x, int z) => (HashBits(seed, x, z) & 0xffffff) / 16777216f;
    }
}

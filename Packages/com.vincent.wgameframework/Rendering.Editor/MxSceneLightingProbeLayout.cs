using System;
using System.Collections.Generic;
using MxFramework.Rendering;
using UnityEngine;

namespace MxFramework.Rendering.Editor
{
    /// <summary>Deterministic budgeted layout shared by preview and capture. Allocates offline only.</summary>
    public sealed class MxSceneLightingProbeLayout
    {
        public Vector3[] Positions { get; private set; }
        public float[] LocalSpacing { get; private set; }
        public MxSceneLightingProbeTopology Topology { get; private set; }
        public MxSceneLightingProbeGrid Grid { get; private set; }
        // Per base cell: 0 coarse, 1 manual, 2 automatic, -1 excluded.
        public int[] CellKinds { get; private set; }
        public float[] CellScores { get; private set; }
        public int ManualBricks { get; private set; }
        public int AutomaticBricks { get; private set; }
        public int RejectedForBudget { get; private set; }
        public int AutomaticCandidates { get; private set; }
        public string Diagnostics { get; private set; }
        private struct Candidate { public int Id; public bool Manual; public float Score; }

        public static MxSceneLightingProbeLayout Create(MxSceneLightingProbeGrid grid, MxSceneLightingProbeQuality quality,
            MxSceneLightingProbeDemand demand = null)
        {
            bool enabled = quality != null && quality.Enabled;
            int budget = enabled ? Mathf.Clamp(quality.MaximumProbeCount, 100, 120000) : 120000;
            float requestedSpacing = grid.Spacing;
            if (enabled && quality.AutomaticRefinement)
            {
                Bounds coverage = new Bounds(grid.Origin + (Vector3)(grid.Dimensions - Vector3Int.one) * (grid.Spacing * .5f),
                    (Vector3)(grid.Dimensions - Vector3Int.one) * grid.Spacing - Vector3.one * (2 * grid.Spacing));
                for (int attempt = 0; grid.Count > budget && attempt < 64; attempt++)
                    grid = MxSceneLightingBuilder.ProbeGrid(grid.CaptureBounds, grid.Spacing * 1.25f, coverage);
            }
            if (grid.Count > budget) throw new InvalidOperationException("Base probe lattice exceeds the configured probe budget.");
            var positions = new List<Vector3>((int)grid.Count); var spacing = new List<float>((int)grid.Count);
            var ids = new Dictionary<Vector3Int, int>();
            for (int id = 0; id < grid.Count; id++)
            {
                positions.Add(grid.Position(id)); spacing.Add(grid.Spacing);
                if (enabled) ids.Add(new Vector3Int(id % grid.Dimensions.x, (id / grid.Dimensions.x) % grid.Dimensions.y,
                    id / (grid.Dimensions.x * grid.Dimensions.y)) * 2, id);
            }
            var result = new MxSceneLightingProbeLayout { Grid = grid };
            if (enabled)
            {
                var topology = new MxSceneLightingProbeTopology { CellDimensions = grid.Dimensions - Vector3Int.one };
                Vector3Int dims = topology.CellDimensions; int cells = checked(dims.x * dims.y * dims.z);
                topology.CellBricks = new int[cells]; result.CellKinds = new int[cells]; result.CellScores = new float[cells];
                for (int i = 0; i < cells; i++) topology.CellBricks[i] = -1;
                ValidateVolumes(quality.LocalRefinementVolumes); ValidateVolumes(quality.RefinementExclusionVolumes);
                ValidateVolumes(quality.ActivityVolumes); ValidateVolumes(quality.MediumVolumes);
                if (quality.AutomaticRefinement && demand == null)
                    throw new ArgumentException("Automatic layout requires a geometry/demand snapshot; use the builder planner.");
                var candidates = new List<Candidate>();
                for (int z = 0; z < dims.z; z++) for (int y = 0; y < dims.y; y++) for (int x = 0; x < dims.x; x++)
                {
                    int id = x + dims.x * (y + dims.y * z);
                    var bounds = new Bounds(grid.Origin + new Vector3(x + .5f, y + .5f, z + .5f) * grid.Spacing, Vector3.one * grid.Spacing);
                    if (MxSceneLightingProbeDemand.Intersects(bounds, quality.RefinementExclusionVolumes)) { result.CellKinds[id] = -1; continue; }
                    bool manual = quality.UseManualRefinement && MxSceneLightingProbeDemand.Intersects(bounds, quality.LocalRefinementVolumes);
                    float score = quality.AutomaticRefinement ? demand.Score(bounds, quality) : 0; result.CellScores[id] = score;
                    if (!manual && (!quality.AutomaticRefinement || score < quality.MinimumAutomaticScore)) continue;
                    if (!manual) result.AutomaticCandidates++;
                    candidates.Add(new Candidate { Id = id, Manual = manual, Score = score });
                }
                candidates.Sort((a,b) => {
                    int priority = b.Manual.CompareTo(a.Manual); if (priority != 0) return priority;
                    int score = b.Score.CompareTo(a.Score); return score != 0 ? score : a.Id.CompareTo(b.Id);
                });
                var bricks = new List<int>(); int automaticLimit = -1;
                for (int index = 0; index < candidates.Count; index++)
                {
                    // Shared half-spacing points make adjacent refinements cheaper. Rank
                    // automatic demand by its actual remaining point cost, not by a fixed
                    // per-cell cost that spends the same budget on scattered tiny islands.
                    if (!candidates[index].Manual)
                    {
                        int best = index; float bestValue = -1;
                        for (int next = index; next < candidates.Count; next++)
                        {
                            var choice = candidates[next];
                            int nextCost = RefinementCost(choice.Id, dims, ids);
                            float value = choice.Score / Mathf.Max(1, nextCost);
                            if (value > bestValue || (value == bestValue && choice.Id < candidates[best].Id))
                            { best = next; bestValue = value; }
                        }
                        var swap = candidates[index]; candidates[index] = candidates[best]; candidates[best] = swap;
                    }
                    var candidate = candidates[index];
                    if (!candidate.Manual && automaticLimit < 0)
                        automaticLimit = positions.Count + Mathf.FloorToInt((budget - positions.Count) * quality.AutomaticBudgetFraction);
                    int limit = candidate.Manual ? budget : automaticLimit;
                    int x = candidate.Id % dims.x, y = (candidate.Id / dims.x) % dims.y, z = candidate.Id / (dims.x * dims.y);
                    var cell = new Vector3Int(x, y, z); int cost = 0;
                    for (int fz = 0; fz < 3; fz++) for (int fy = 0; fy < 3; fy++) for (int fx = 0; fx < 3; fx++)
                        if (!ids.ContainsKey(cell * 2 + new Vector3Int(fx, fy, fz))) cost++;
                    if (positions.Count + cost > limit)
                    {
                        if (!quality.AutomaticRefinement) throw new InvalidOperationException("Local refinement exceeds the configured probe budget; reduce local volumes.");
                        result.RejectedForBudget++; continue;
                    }
                    topology.CellBricks[candidate.Id] = bricks.Count / 27; result.CellKinds[candidate.Id] = candidate.Manual ? 1 : 2;
                    if (candidate.Manual) result.ManualBricks++; else result.AutomaticBricks++;
                    for (int fz = 0; fz < 3; fz++) for (int fy = 0; fy < 3; fy++) for (int fx = 0; fx < 3; fx++)
                    {
                        var key = cell * 2 + new Vector3Int(fx, fy, fz);
                        if (!ids.TryGetValue(key, out int id))
                        {
                            id = positions.Count; ids.Add(key, id);
                            positions.Add(grid.Origin + (Vector3)key * (grid.Spacing * .5f)); spacing.Add(grid.Spacing * .5f);
                        }
                        else spacing[id] = Mathf.Min(spacing[id], grid.Spacing * .5f);
                        bricks.Add(id);
                    }
                }
                topology.BrickIndices = bricks.ToArray(); topology.Validate(grid.Dimensions, positions.Count); result.Topology = topology;
            }
            result.Positions = positions.ToArray(); result.LocalSpacing = spacing.ToArray();
            result.Diagnostics = $"基础 {grid.Count} 点；人工 {result.ManualBricks} 格；自动 {result.AutomaticBricks}/{result.AutomaticCandidates} 格；预算略过 {result.RejectedForBudget} 格；实际 {positions.Count}/{budget} 点。" +
                (grid.Spacing != requestedSpacing ? $" 基础预算不足：间距从 {requestedSpacing:g}m 降级为 {grid.Spacing:g}m，完整插值网格保留。" : "") +
                (enabled && quality.AutomaticRefinement ? $" 自动使用剩余预算的 {quality.AutomaticBudgetFraction:P0}；评分是几何/需求启发式，非照明误差。" : "");
            return result;
        }
        private static void ValidateVolumes(Bounds[] volumes)
        {
            foreach (var volume in volumes ?? Array.Empty<Bounds>()) for (int axis = 0; axis < 3; axis++)
                if (!Finite(volume.center[axis]) || !Finite(volume.size[axis]) || volume.size[axis] <= 0)
                    throw new ArgumentException("Probe demand volumes require finite coordinates and positive size.");
        }
        private static int RefinementCost(int id, Vector3Int dims, Dictionary<Vector3Int, int> ids)
        {
            var cell = new Vector3Int(id % dims.x, (id / dims.x) % dims.y, id / (dims.x * dims.y));
            int cost = 0;
            for (int z = 0; z < 3; z++) for (int y = 0; y < 3; y++) for (int x = 0; x < 3; x++)
                if (!ids.ContainsKey(cell * 2 + new Vector3Int(x, y, z))) cost++;
            return cost;
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

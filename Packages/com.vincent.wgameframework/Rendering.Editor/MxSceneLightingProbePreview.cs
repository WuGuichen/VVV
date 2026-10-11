using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Rendering.Editor
{
    /// <summary>Editor-owned CPU preview. Refresh is explicit/low frequency; drawing never decodes transport.
    /// Dispose releases the borrowed asset reference and decoded payload, without changing assets or runtime providers.</summary>
    public sealed class MxSceneLightingProbePreview : IDisposable
    {
        public MxSceneLightingProbeGrid PlannedGrid { get; private set; }
        public MxSceneLightingProbeGrid CachedGrid { get; private set; }
        public bool HasPlan { get; private set; }
        public bool HasCache => Payload != null;
        public bool CacheCurrent { get; private set; }
        public string PlanIssue { get; private set; } = string.Empty;
        public string CacheIssue { get; private set; } = string.Empty;
        public MxSceneLightingPayload Payload { get; private set; }
        public int ValidCount { get; private set; }
        public int DecodeCount { get; private set; }
        public Bounds ActualCacheBounds { get; private set; }
        public int OuterCount { get; private set; }
        public int OuterValidCount { get; private set; }
        public int RelocatedCount { get; private set; }
        public int DilatedCount { get; private set; }
        public string CacheDiagnostics => !HasCache ? string.Empty :
            $"格式 v{Payload.Version}；实际点位范围 {ActualCacheBounds.min:F3} → {ActualCacheBounds.max:F3}；间距 {CachedGrid.Spacing:g}；有效率 {100f * ValidCount / Mathf.Max(1,Payload.Probes.Length):F2}%；边界层有效 {OuterValidCount}/{OuterCount}；细分砖 {Payload.Topology?.BrickCount??0}；修正 {RelocatedCount}；补偿 {DilatedCount}。有效标记不代表无遮挡或理想照明。";
        public string FilterDiagnostics { get; private set; } = string.Empty;
        public MxSceneLightingProbeLayout PlannedLayout { get; private set; }
        public long PlannedPointCount => PlannedLayout?.Positions.LongLength ?? PlannedGrid.Count;
        private MxSceneLightingData _data;
        private Hash128 _dataHash, _payloadHash;
        private string _metadata;
        private string _planKey, _readIssue;

        public void Refresh(MxSceneRenderingConfiguration configuration, bool forcePlan = false)
        {
            try
            {
                if (configuration?.StaticRoot == null) { HasPlan = false; _planKey = null; PlanIssue = "未绑定静态场景根。"; }
                else
                {
                    string key = configuration.SourceLayout == null ? null :
                        MxSceneLightingBuilder.Fingerprint(configuration.StaticRoot, configuration.SourceLayout,
                            configuration.StaticSourceRenderers) + "|" + configuration.ProbeSpacing.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                            "|" + MxSceneLightingBuilder.ProbeSettingsFingerprint(configuration);
                    if (forcePlan || !HasPlan || key == null || key != _planKey)
                    {
                        PlannedGrid = MxSceneLightingBuilder.PlanProbeGrid(configuration.StaticRoot,
                            configuration.StaticSourceRenderers, configuration.ProbeSpacing,
                            probeCoverageBounds: MxSceneLightingBuilder.ProbeCoverage(configuration));
                        PlannedLayout=null;
                        try {PlannedLayout = MxSceneLightingBuilder.PlanProbeLayout(configuration);PlannedGrid=PlannedLayout.Grid;PlanIssue=PlannedLayout.Diagnostics;}
                        catch(InvalidOperationException e){PlanIssue=e.Message;}
                        HasPlan = true; _planKey = key;
                    }
                }
            }
            catch (Exception e) { HasPlan = false; PlanIssue = e.Message; }

            CacheCurrent = MxSceneLightingBuilder.CacheMatchesSource(configuration, out string reason);
            CacheIssue = reason;
            MxSceneLightingData data = configuration?.Data;
            if (data == null) { ClearCache(); CacheIssue = "未绑定光传输缓存；可查看计划范围，有效性未知。"; return; }
            Hash128 dataHash = Dependency(data), payloadHash = Dependency(data.Payload);
            string metadata = JsonUtility.ToJson(data);
            if (_data == data && _dataHash == dataHash && _payloadHash == payloadHash && _metadata == metadata)
            { if (_readIssue != null) { CacheCurrent = false; CacheIssue = _readIssue; } return; }
            ClearCache(); _data = data; _dataHash = dataHash; _payloadHash = payloadHash; _metadata = metadata;
            try
            {
                Payload = data.ReadPayload();
                CachedGrid = new MxSceneLightingProbeGrid(data.Bounds, data.ProbeOrigin, data.ProbeDimensions, data.ProbeSpacing);
                Bounds actual = new Bounds((Vector3)Payload.Probes[0].PositionValid,Vector3.zero);
                for (int id=0;id<Payload.Probes.Length;id++)
                {
                    var probe=Payload.Probes[id]; actual.Encapsulate((Vector3)probe.PositionValid);
                    if (probe.PositionValid.w > .5f) ValidCount++;
                    if(Payload.Version==2)
                    {if(Payload.ProbeQuality[id].x>.0001f)RelocatedCount++;if(Payload.DilationRows[id].Count>0)DilatedCount++;}
                    int x=id % data.ProbeDimensions.x, z=id/(data.ProbeDimensions.x*data.ProbeDimensions.y);
                    if (id<(long)data.ProbeDimensions.x*data.ProbeDimensions.y*data.ProbeDimensions.z &&
                        (x==0 || x==data.ProbeDimensions.x-1 || z==0 || z==data.ProbeDimensions.z-1))
                    { OuterCount++; if (probe.PositionValid.w > .5f) OuterValidCount++; }
                }
                ActualCacheBounds=actual;
                DecodeCount++;
            }
            catch (Exception e) { Payload = null; CacheCurrent = false; CacheIssue = _readIssue = "缓存无法读取：" + e.Message; }
        }

        private static Hash128 Dependency(UnityEngine.Object asset)
        {
            string path = AssetDatabase.GetAssetPath(asset);
            return string.IsNullOrEmpty(path) ? default : AssetDatabase.GetAssetDependencyHash(path);
        }
        private void ClearCache() { Payload = null; _data = null; _metadata = null; _readIssue = null; ValidCount = OuterCount = OuterValidCount = RelocatedCount = DilatedCount = 0; ActualCacheBounds = default; }
        public void Dispose() { ClearCache(); HasPlan = false; PlannedLayout=null; }

        public int PointCount(bool cached) => cached ? Payload?.Probes.Length ?? 0 : HasPlan ? PlannedLayout?.Positions.Length ?? 0 : 0;
        public Vector3 Position(bool cached, int index) => cached ? (Vector3)Payload.Probes[index].PositionValid : PlannedLayout.Positions[index];
        public bool IsValid(int index) => Payload.Probes[index].PositionValid.w > .5f;
        public string QualityDescription(int index)
        {
            if(Payload.Version!=2)return "v1 缓存未记录点位修正、最小距离或补偿；启用质量优化后需重建。";
            var quality=Payload.ProbeQuality[index];
            return $"点位修正 {quality.x:F4}m；表面最小距离 {quality.y:F4}m；背面比例 {quality.z:P2}；补偿源 {Payload.DilationRows[index].Count}（贡献 {quality.w:P0}）；所选分箱最近捕获距离由 v2 保存。";
        }

        /// <summary>Two bounded scans, no allocation after the caller's list is sized.
        /// Filtering precedes deterministic thinning so the display cap covers the whole matching slice.</summary>
        public int CollectVisible(bool cached, bool slice, float height, float halfThickness,
            bool local, Vector3 center, float radius, int validity, int limit, List<int> output)
        {
            output.Clear(); int count = PointCount(cached), matching = 0, afterHeight = 0, afterLocal = 0;
            float lowY=float.PositiveInfinity, highY=float.NegativeInfinity;
            bool HeightMatches(Vector3 p) => !slice || Mathf.Abs(p.y-height) <= Mathf.Max(0,halfThickness)+SliceTolerance(p.y,height);
            bool Matches(int i)
            {
                Vector3 p = Position(cached, i);
                if (!HeightMatches(p)) return false;
                if (local && (p - center).sqrMagnitude > Mathf.Max(0, radius) * Mathf.Max(0, radius)) return false;
                return !cached || validity == 0 || (validity == 1 ? IsValid(i) : !IsValid(i));
            }
            for (int i = 0; i < count; i++)
            {
                Vector3 p=Position(cached,i);
                if (!HeightMatches(p)) continue;
                afterHeight++;lowY=Mathf.Min(lowY,p.y);highY=Mathf.Max(highY,p.y);
                if (local && (p-center).sqrMagnitude > Mathf.Max(0,radius)*Mathf.Max(0,radius)) continue;
                afterLocal++;
                if (!cached || validity==0 || (validity==1 ? IsValid(i) : !IsValid(i))) matching++;
            }
            limit = Mathf.Clamp(limit, 1, 2000);
            int stride = Mathf.Max(1, Mathf.CeilToInt(matching / (float)limit)), ordinal = 0;
            for (int i = 0; i < count; i++) if (Matches(i) && ordinal++ % stride == 0) output.Add(i);
            string range=afterHeight==0 ? "无匹配高度" : $"实际匹配 Y {lowY:F6}–{highY:F6}";
            FilterDiagnostics=$"总点 {count} → 高度 {afterHeight} → 局部范围 {afterLocal} → 有效性 {matching} → 绘制 {output.Count}（上限 {limit}，步长 {stride}）；"+
                (slice ? $"闭区间 [{height-Mathf.Max(0,halfThickness):F6}, {height+Mathf.Max(0,halfThickness):F6}]；" : "全部高度；")+range;
            return matching;
        }

        /// <summary>Four single-precision ULPs at the slice coordinate scale, with a 10-micrometre floor.</summary>
        public static float SliceTolerance(float positionY,float centerY)
            => Mathf.Max(.00001f,Mathf.Max(Mathf.Abs(positionY),Mathf.Abs(centerY))*4.7683716e-7f);

        /// <summary>World-space centre direction of a persisted 6-face x 4x4 visibility bin.
        /// This is a bin representative, not one of the original capture rays.</summary>
        public static Vector3 VisibilityDirection(int bin)
        {
            if (bin < 0 || bin >= 96) throw new ArgumentOutOfRangeException(nameof(bin));
            float u = ((bin % 4) + .5f) * .5f - 1, v = (((bin / 4) % 4) + .5f) * .5f - 1;
            switch (bin / 16)
            {
                case 0: return new Vector3(1, v, -u).normalized;
                case 1: return new Vector3(-1, v, u).normalized;
                case 2: return new Vector3(u, 1, -v).normalized;
                case 3: return new Vector3(u, -1, v).normalized;
                case 4: return new Vector3(u, v, 1).normalized;
                default: return new Vector3(-u, v, -1).normalized;
            }
        }
    }
}

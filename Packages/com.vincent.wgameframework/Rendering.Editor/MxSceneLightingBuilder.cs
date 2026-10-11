using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MxFramework.Rendering.Editor
{
    /// <summary>Uniform candidate lattice; validity is unknown until transport capture.</summary>
    public readonly struct MxSceneLightingProbeGrid
    {
        public readonly Bounds CaptureBounds;
        public readonly Vector3 Origin;
        public readonly Vector3Int Dimensions;
        public readonly float Spacing;
        public long Count => checked((long)Dimensions.x * Dimensions.y * Dimensions.z);
        public Bounds GridBounds => new Bounds(Origin + (Vector3)(Dimensions - Vector3Int.one) * (Spacing * .5f),
            (Vector3)(Dimensions - Vector3Int.one) * Spacing);
        public MxSceneLightingProbeGrid(Bounds captureBounds, Vector3 origin, Vector3Int dimensions, float spacing)
        { CaptureBounds = captureBounds; Origin = origin; Dimensions = dimensions; Spacing = spacing; }
        public Vector3 Position(int index)
        {
            if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
            int x = index % Dimensions.x, y = (index / Dimensions.x) % Dimensions.y;
            int z = (int)(index / ((long)Dimensions.x * Dimensions.y));
            return Origin + new Vector3(x, y, z) * Spacing;
        }
    }

    /// <summary>Editor-only hemicube radiosity compiler. Never bakes a particular lighting setup.</summary>
    public static class MxSceneLightingBuilder
    {
        private static BuildJob _job;
        private static bool _reloadLocked;
        public static bool IsBuilding => _job!=null;
        public static string Status { get; private set; }="Idle";
        public static string LastError { get; private set; }=string.Empty;
        public static MxSceneLightingData LastResult { get; private set; }

        /// <summary>Low-frequency authoring query. No capture, material readback or asset writes.
        /// Uses the same source selection, triangle subdivision/rejection and lattice rules as Begin.</summary>
        public static MxSceneLightingProbeGrid PlanProbeGrid(Transform root,
            IReadOnlyList<MeshRenderer> selectedRenderers = null,
            float probeSpacing = MxSceneRenderingConfiguration.DefaultProbeSpacing, float surfaceCellSize = 3f,
            Bounds? probeCoverageBounds = null)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            float cell = NormalizeSpacing(surfaceCellSize);
            var extent = new CaptureExtent();
            foreach (var renderer in Sources(root, selectedRenderers))
            {
                if (!renderer.enabled || !renderer.TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh == null) continue;
                Mesh mesh = filter.sharedMesh;
                Vector3[] vertices = mesh.vertices;
                Matrix4x4 matrix = renderer.localToWorldMatrix;
                bool mirrored = matrix.determinant < 0;
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    int[] indices = mesh.GetTriangles(sub);
                    for (int t = 0; t < indices.Length; t += 3)
                        IncludeCaptureTriangle(extent, matrix.MultiplyPoint3x4(vertices[indices[t]]),
                            matrix.MultiplyPoint3x4(vertices[indices[t + (mirrored ? 2 : 1)]]),
                            matrix.MultiplyPoint3x4(vertices[indices[t + (mirrored ? 1 : 2)]]), cell, 0);
                }
            }
            return ProbeGrid(extent.Bounds, probeSpacing, probeCoverageBounds);
        }

        /// <summary>Shared lattice formula. Positive finite spacings below one are clamped to one.</summary>
        public static MxSceneLightingProbeGrid ProbeGrid(Bounds bounds, float spacing, Bounds? probeCoverageBounds = null)
        {
            spacing = NormalizeSpacing(spacing);
            Bounds coverage = probeCoverageBounds ?? bounds;
            if (probeCoverageBounds.HasValue) ValidateCoverage(coverage);
            Vector3 span = coverage.size + Vector3.one * (spacing * 2);
            var dimensions = new Vector3Int(Mathf.CeilToInt(span.x / spacing) + 1,
                Mathf.CeilToInt(span.y / spacing) + 1, Mathf.CeilToInt(span.z / spacing) + 1);
            if (dimensions.x < 1 || dimensions.y < 1 || dimensions.z < 1)
                throw new InvalidOperationException("Scene lighting probe grid is too large.");
            var grid = new MxSceneLightingProbeGrid(bounds, coverage.min - Vector3.one * spacing, dimensions, spacing);
            _ = grid.Count; // Check multiplication before the compiler or preview consumes it.
            return grid;
        }

        /// <summary>Preserves legacy source fingerprints; separately validates the authored lattice spacing.</summary>
        public static bool CacheMatchesSource(MxSceneRenderingConfiguration configuration, out string reason)
        {
            if (configuration?.Data == null || configuration.StaticRoot == null || configuration.SourceLayout == null)
            { reason = "Light transport source or cache is missing."; return false; }
            try
            {
                if (NormalizeSpacing(configuration.ProbeSpacing) != configuration.Data.RequestedProbeSpacing)
                { reason = "Probe spacing changed. Rebuild Light Transport."; return false; }
                if (ProbeSettingsFingerprint(configuration) != configuration.Data.ProbeSettingsFingerprint)
                { reason = "Probe coverage or quality settings changed. Rebuild Light Transport."; return false; }
                if (Fingerprint(configuration.StaticRoot, configuration.SourceLayout, configuration.StaticSourceRenderers) != configuration.Data.SourceFingerprint)
                { reason = "Static geometry or reflectance changed. Rebuild Light Transport."; return false; }
                reason = string.Empty; return true;
            }
            catch (Exception e) { reason = e.Message; return false; }
        }

        private static float NormalizeSpacing(float spacing)
        {
            if (float.IsNaN(spacing) || float.IsInfinity(spacing)) throw new ArgumentOutOfRangeException(nameof(spacing));
            return Mathf.Max(1, spacing);
        }

        /// <summary>Dilation never crosses static triangles and requires conservative visibility
        /// in both directions. Only directly valid captures may supply coefficients.</summary>
        public static bool VisibleDilationSegment(MxSceneLightingPayload payload,int target,int source,MxSceneLightingProbeGeometry geometry)
        {
            if(payload?.Version!=2 || target==source || target<0 || source<0 || target>=payload.Probes.Length || source>=payload.Probes.Length ||
                payload.Probes[source].PositionValid.w!=1 || payload.ProbeQuality[target].z>.55f)return false;
            Vector3 a=payload.Probes[target].PositionValid,b=payload.Probes[source].PositionValid,delta=b-a;float distance=delta.magnitude;
            if(distance<=.0001f || !geometry.SegmentClear(a,b))return false;
            int Bin(Vector3 d)
            {
                Vector3 abs=new Vector3(Mathf.Abs(d.x),Mathf.Abs(d.y),Mathf.Abs(d.z));int face;Vector2 uv;
                if(abs.x>=abs.y && abs.x>=abs.z){face=d.x>=0?0:1;uv=new Vector2(d.x>=0?-d.z:d.z,d.y)/abs.x;}
                else if(abs.y>=abs.z){face=d.y>=0?2:3;uv=new Vector2(d.x,d.y>=0?-d.z:d.z)/abs.y;}
                else {face=d.z>=0?4:5;uv=new Vector2(d.z>=0?d.x:-d.x,d.y)/abs.z;}
                return face*16+Mathf.Clamp((int)((uv.y*.5f+.5f)*4),0,3)*4+Mathf.Clamp((int)((uv.x*.5f+.5f)*4),0,3);
            }
            return payload.ProbeClearDistances[target*96+Bin(delta)]>=distance+.025f &&
                payload.ProbeClearDistances[source*96+Bin(-delta)]>=distance+.025f;
        }

        public static Bounds? ProbeCoverage(MxSceneRenderingConfiguration configuration)
            => configuration != null && configuration.UseProbeCoverageBounds ? configuration.ProbeCoverageBounds : (Bounds?)null;

        public static string ProbeSettingsFingerprint(MxSceneRenderingConfiguration configuration)
            => QualityFingerprint(ProbeCoverage(configuration),ResolveProbeQuality(configuration));

        /// <summary>Allocating authoring snapshot. Medium is world-space and independent of camera/focus clearing.</summary>
        public static MxSceneLightingProbeQuality ResolveProbeQuality(MxSceneRenderingConfiguration configuration)
        {
            var quality = configuration?.ProbeQuality;
            if (quality == null || !quality.AutomaticRefinement) return quality;
            var result = JsonUtility.FromJson<MxSceneLightingProbeQuality>(JsonUtility.ToJson(quality));
            result.MediumDemandEnabled = configuration.HeightFogEnabled || configuration.DustEnabled;
            result.MediumDensity = configuration.HeightFogEnabled ? configuration.FogDensity : 0;
            result.MediumBaseHeight = configuration.FogBaseHeight;
            result.MediumHeightFalloff = configuration.FogHeightFalloff;
            result.MediumVolumes = Array.Empty<Bounds>();
            if (configuration.DustEnabled && configuration.Dust != null)
            {
                var bounds = ProbeCoverage(configuration) ?? PlanProbeGrid(configuration.StaticRoot, configuration.StaticSourceRenderers,
                    configuration.ProbeSpacing).GridBounds;
                Vector3 center = bounds.center, size = bounds.size;
                center.y = configuration.Dust.BaseHeight + configuration.Dust.Height * .5f;
                size.y = Mathf.Max(.1f, configuration.Dust.Height);
                result.MediumVolumes = new[] { new Bounds(center, size) };
            }
            return result;
        }

        public static MxSceneLightingProbeLayout PlanProbeLayout(MxSceneRenderingConfiguration configuration)
        {
            var quality = ResolveProbeQuality(configuration);
            var grid = PlanProbeGrid(configuration.StaticRoot, configuration.StaticSourceRenderers,
                configuration.ProbeSpacing, probeCoverageBounds: ProbeCoverage(configuration));
            return MxSceneLightingProbeLayout.Create(grid, quality, CreateProbeDemand(configuration.StaticRoot,configuration.StaticSourceRenderers,quality));
        }

        private static MxSceneLightingProbeDemand CreateProbeDemand(Transform root,IReadOnlyList<MeshRenderer> selection,MxSceneLightingProbeQuality quality)
        {
            if (quality?.Enabled == true && quality.AutomaticRefinement)
            {
                Mesh mesh = BuildOccluders(root, selection);
                try
                {
                    var vertices = mesh.vertices; var indices = mesh.triangles; var triangles = new Vector3[indices.Length];
                    for (int i = 0; i < indices.Length; i++) triangles[i] = vertices[indices[i]];
                    return new MxSceneLightingProbeDemand(triangles,
                        quality.AutomaticNavigation ? MxSceneLightingProbeDemand.NavigationSnapshot() : null);
                }
                finally { UnityEngine.Object.DestroyImmediate(mesh); }
            }
            return null;
        }

        [Serializable] private sealed class LegacyProbeQuality
        {
            public bool Enabled;
            public Bounds[] LocalRefinementVolumes;
            public bool RelocateNearGeometry;
            public float MinimumClearance, MaximumRelocationFraction;
            public bool VisibilityConstrainedDilation;
            public int MaximumProbeCount;
        }

        private static string QualityFingerprint(Bounds? coverage,MxSceneLightingProbeQuality quality)
        {
            string coverageKey=CoverageFingerprint(coverage);
            if(quality==null || !quality.Enabled)return coverageKey;
            ValidateQuality(quality);
            bool legacy = !quality.AutomaticRefinement && quality.UseManualRefinement && (quality.RefinementExclusionVolumes?.Length ?? 0) == 0;
            string settings = legacy ? JsonUtility.ToJson(new LegacyProbeQuality { Enabled=quality.Enabled,
                LocalRefinementVolumes=quality.LocalRefinementVolumes, RelocateNearGeometry=quality.RelocateNearGeometry,
                MinimumClearance=quality.MinimumClearance, MaximumRelocationFraction=quality.MaximumRelocationFraction,
                VisibilityConstrainedDilation=quality.VisibilityConstrainedDilation, MaximumProbeCount=quality.MaximumProbeCount }) : JsonUtility.ToJson(quality);
            string text=(legacy ? "probe.quality.v2|" : "probe.automatic.v2|")+coverageKey+"|"+settings;
            if (quality.AutomaticRefinement && quality.AutomaticNavigation)
                text += "|navigation|" + MxSceneLightingProbeDemand.NavigationFingerprint();
            using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-",string.Empty);
        }

        private static void ValidateQuality(MxSceneLightingProbeQuality quality)
        {
            if(float.IsNaN(quality.MinimumClearance)||float.IsInfinity(quality.MinimumClearance)||quality.MinimumClearance<.025f ||
                float.IsNaN(quality.MaximumRelocationFraction)||float.IsInfinity(quality.MaximumRelocationFraction)||
                quality.MaximumRelocationFraction<0 || quality.MaximumRelocationFraction>.45f)
                throw new ArgumentException("Probe clearance must be >= 0.025 and relocation fraction in [0,0.45].");
            foreach(var volume in quality.LocalRefinementVolumes??Array.Empty<Bounds>())ValidateCoverage(volume);
            foreach(var volume in quality.RefinementExclusionVolumes??Array.Empty<Bounds>())ValidateCoverage(volume);
            foreach(var volume in quality.ActivityVolumes??Array.Empty<Bounds>())ValidateCoverage(volume);
            foreach(var volume in quality.MediumVolumes??Array.Empty<Bounds>())ValidateCoverage(volume);
            if (quality.AutomaticRefinement &&
                (!Finite(quality.ReceiverDistanceFraction) || quality.ReceiverDistanceFraction < .1f || quality.ReceiverDistanceFraction > 2 ||
                !Finite(quality.AutomaticBudgetFraction) || quality.AutomaticBudgetFraction < 0 || quality.AutomaticBudgetFraction > 1 ||
                !Finite(quality.MinimumAutomaticScore) || quality.MinimumAutomaticScore <= 0 ||
                !Finite(quality.MediumDensity) || quality.MediumDensity < 0 || !Finite(quality.MediumBaseHeight) ||
                !Finite(quality.MediumHeightFalloff) || quality.MediumHeightFalloff < 0))
                throw new ArgumentException("Automatic probe settings require finite values and valid ranges.");
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static string CoverageFingerprint(Bounds? coverage)
        {
            if (!coverage.HasValue) return string.Empty; // Existing cache identity remains readable.
            ValidateCoverage(coverage.Value);
            var b = coverage.Value;
            var text = new StringBuilder("probe.coverage.v1");
            foreach (float value in new[] { b.center.x,b.center.y,b.center.z,b.size.x,b.size.y,b.size.z })
                text.Append('|').Append(value.ToString("R",CultureInfo.InvariantCulture));
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-",string.Empty);
        }

        private static void ValidateCoverage(Bounds bounds)
        {
            for (int axis=0;axis<3;axis++)
                if (float.IsNaN(bounds.center[axis]) || float.IsInfinity(bounds.center[axis]) ||
                    float.IsNaN(bounds.size[axis]) || float.IsInfinity(bounds.size[axis]) || bounds.size[axis]<=0)
                    throw new ArgumentException("Probe coverage requires finite world coordinates and positive size.");
        }

        private sealed class CaptureExtent
        {
            private Vector3 _min, _max;
            private bool _hasVertices;
            public Bounds Bounds
            {
                get
                {
                    if (!_hasVertices) throw new InvalidOperationException("No non-degenerate static capture triangles.");
                    var bounds = new Bounds(); bounds.SetMinMax(_min, _max); return bounds;
                }
            }
            public void Include(Vector3 point)
            {
                if (!_hasVertices) { _min = _max = point; _hasVertices = true; }
                else { _min = Vector3.Min(_min, point); _max = Vector3.Max(_max, point); }
            }
        }

        private static int SplitCaptureTriangle(Vector3 a, Vector3 b, Vector3 c, float cell, int depth)
        {
            float ab = (b-a).sqrMagnitude, bc = (c-b).sqrMagnitude, ca = (a-c).sqrMagnitude;
            if (depth >= 8 || Mathf.Max(ab, Mathf.Max(bc, ca)) <= cell * cell * 2.25f) return -1;
            return ab >= bc && ab >= ca ? 0 : bc >= ca ? 1 : 2;
        }
        private static bool CapturableTriangle(Vector3 a, Vector3 b, Vector3 c)
            => Vector3.Cross(b-a, c-a).magnitude * .5f >= .00001f;
        private static void IncludeCaptureTriangle(CaptureExtent extent, Vector3 a, Vector3 b, Vector3 c, float cell, int depth)
        {
            int split = SplitCaptureTriangle(a, b, c, cell, depth);
            if (split == 0) { Vector3 m=(a+b)*.5f; IncludeCaptureTriangle(extent,a,m,c,cell,depth+1); IncludeCaptureTriangle(extent,m,b,c,cell,depth+1); }
            else if (split == 1) { Vector3 m=(b+c)*.5f; IncludeCaptureTriangle(extent,a,b,m,cell,depth+1); IncludeCaptureTriangle(extent,a,m,c,cell,depth+1); }
            else if (split == 2) { Vector3 m=(c+a)*.5f; IncludeCaptureTriangle(extent,a,b,m,cell,depth+1); IncludeCaptureTriangle(extent,m,b,c,cell,depth+1); }
            else if (CapturableTriangle(a,b,c)) { extent.Include(a); extent.Include(b); extent.Include(c); }
        }

        public static void Begin(Transform root,string outputFolder,MxSceneLightingSourceLayout layout,float surfaceCellSize=3f,float probeSpacing=4f)
            => Begin(root, outputFolder, layout, null, surfaceCellSize, probeSpacing);

        public static void Begin(Transform root,string outputFolder,MxSceneLightingSourceLayout layout,
            IReadOnlyList<MeshRenderer> selectedRenderers,float surfaceCellSize=3f,float probeSpacing=4f,
            Bounds? probeCoverageBounds=null,MxSceneLightingProbeQuality probeQuality=null)
        {
            if(_job!=null)throw new InvalidOperationException("A scene-lighting build is already running.");
            if(Application.isPlaying)throw new InvalidOperationException("Compile static lighting in Edit Mode.");
            if(root==null)throw new ArgumentNullException(nameof(root));
            LastError=string.Empty;LastResult=null;
            if(layout==null || string.IsNullOrWhiteSpace(layout.BaseColorProperty) || string.IsNullOrWhiteSpace(layout.SurfaceDataProperty))
                throw new ArgumentException("Explicit material source layout is required.");
            _job=new BuildJob(root,outputFolder,layout,selectedRenderers,surfaceCellSize,probeSpacing,probeCoverageBounds,probeQuality);
            EditorApplication.LockReloadAssemblies();_reloadLocked=true;
            EditorApplication.update+=Tick;
        }
        public static void Cancel() { if(_job==null)return;try{_job.Dispose();}finally{_job=null;EditorApplication.update-=Tick;Unlock();Status="Cancelled";} }
        private static void Unlock(){if(!_reloadLocked)return;_reloadLocked=false;EditorApplication.UnlockReloadAssemblies();}
        private static void Tick()
        {
            if(_job==null)return;
            try
            {
                if(_job.Step(12))
                {
                    LastResult=_job.Save();Status="Complete: "+LastResult.SurfaceCount+" surfaces, "+LastResult.ProbeCount+" probes";
                    _job.Dispose();_job=null;EditorApplication.update-=Tick;Unlock();
                    Debug.Log("Scene lighting build: "+Status);
                }
                else Status=_job.Progress;
            }
            catch(Exception ex) {LastError=ex.ToString();Cancel();Status="Failed";Debug.LogException(ex);}
        }

        public static string Fingerprint(Transform root,MxSceneLightingSourceLayout layout)
            => Fingerprint(root, layout, null);

        public static string Fingerprint(Transform root,MxSceneLightingSourceLayout layout,
            IReadOnlyList<MeshRenderer> selectedRenderers)
        {
            if(root==null)return string.Empty;
            var text=new StringBuilder("mx.sceneLighting.v1\n");
            if(selectedRenderers!=null && selectedRenderers.Count>0)
                text.Append("explicitSources|").Append(selectedRenderers.Count).AppendLine();
            text.Append(layout.BaseColorProperty).Append('|').Append(layout.SurfaceDataProperty);
            if(!string.IsNullOrWhiteSpace(layout.EmissionIntensityProperty))text.Append('|').Append(layout.EmissionIntensityProperty);
            text.Append('|').Append(layout.SolidColorProperty).Append('|').Append(layout.SecondaryColorProperty).Append('|').Append(layout.SecondaryBlendProperty);
            foreach(float fraction in layout.DiffuseFractions)text.Append('|').Append(fraction.ToString("R",CultureInfo.InvariantCulture));
            var dependencies=new Dictionary<string,Hash128>(StringComparer.Ordinal);
            foreach(var renderer in Sources(root,selectedRenderers).OrderBy(r=>PathOf(r.transform,root),StringComparer.Ordinal))
            {
                if(!renderer.enabled || !renderer.TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh==null)continue;
                text.Append(PathOf(renderer.transform,root)).Append('|');
                string meshPath=AssetDatabase.GetAssetPath(filter.sharedMesh);
                text.Append(AssetDatabase.AssetPathToGUID(meshPath)).Append(':').Append(Dependency(meshPath,dependencies));
                Matrix4x4 m=renderer.localToWorldMatrix;
                for(int k=0;k<16;k++)text.Append('|').Append(m[k].ToString("R",CultureInfo.InvariantCulture));
                foreach(Material material in renderer.sharedMaterials)
                {
                    if(material==null)continue;
                    text.Append('|').Append(material.shader.name);
                    if(!string.IsNullOrWhiteSpace(layout.EmissionIntensityProperty))
                        text.Append('|').Append(EmissionIntensity(material,layout).ToString("R",CultureInfo.InvariantCulture));
                    if(!string.IsNullOrEmpty(layout.SolidColorProperty) && material.HasProperty(layout.SolidColorProperty))
                        text.Append('|').Append(SolidColor(material,layout).ToString("F6"));
                    foreach(string property in new[]{layout.BaseColorProperty,layout.SurfaceDataProperty})
                    {
                        Texture texture=material.HasProperty(property)?material.GetTexture(property):null;
                        string path=AssetDatabase.GetAssetPath(texture);
                        text.Append('|').Append(AssetDatabase.AssetPathToGUID(path)).Append(':').Append(Dependency(path,dependencies));
                    }
                }
                text.AppendLine();
            }
            using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-",string.Empty);
        }
        private static Hash128 Dependency(string path,Dictionary<string,Hash128> cache)
        { if(string.IsNullOrEmpty(path))return default;if(!cache.TryGetValue(path,out var hash)){hash=AssetDatabase.GetAssetDependencyHash(path);cache.Add(path,hash);}return hash; }
        private static Color SolidColor(Material material,MxSceneLightingSourceLayout layout)
        {
            Color color=material.GetColor(layout.SolidColorProperty);
            if(!string.IsNullOrEmpty(layout.SecondaryColorProperty) && !string.IsNullOrEmpty(layout.SecondaryBlendProperty) &&
                material.HasProperty(layout.SecondaryColorProperty) && material.HasProperty(layout.SecondaryBlendProperty))
                color=Color.Lerp(color,material.GetColor(layout.SecondaryColorProperty),Mathf.Clamp01(material.GetFloat(layout.SecondaryBlendProperty)));
            return color.linear;
        }
        private static float EmissionIntensity(Material material,MxSceneLightingSourceLayout layout)
        {
            if(string.IsNullOrWhiteSpace(layout.EmissionIntensityProperty) || !material.HasProperty(layout.EmissionIntensityProperty))return 1;
            float value=material.GetFloat(layout.EmissionIntensityProperty);
            if(float.IsNaN(value) || float.IsInfinity(value) || value<0)
                throw new InvalidOperationException("Emission intensity must be finite and non-negative: "+material.name);
            return value;
        }
        private static string PathOf(Transform t,Transform root)
        { var path=t.name;while(t.parent!=null && t!=root){t=t.parent;path=t.name+"/"+path;}return path; }

        private static MeshRenderer[] Sources(Transform root,IReadOnlyList<MeshRenderer> selectedRenderers)
        {
            if(selectedRenderers==null || selectedRenderers.Count==0)return root.GetComponentsInChildren<MeshRenderer>(false);
            var result=new MeshRenderer[selectedRenderers.Count];
            var unique=new HashSet<MeshRenderer>();
            for(int i=0;i<selectedRenderers.Count;i++)
            {
                var renderer=selectedRenderers[i];
                if(renderer==null || !renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                    !renderer.transform.IsChildOf(root) || !renderer.TryGetComponent<MeshFilter>(out var filter) ||
                    filter.sharedMesh==null || !unique.Add(renderer))
                    throw new InvalidOperationException("Explicit static source is missing, inactive, outside the root, or duplicated.");
                result[i]=renderer;
            }
            return result;
        }

        /// <summary>Builds position-only world geometry without transport sampling subdivisions.
        /// The caller owns the returned mesh. Exact position welding preserves thin gaps.</summary>
        public static Mesh BuildOccluders(Transform root)
            => BuildOccluders(root, null);

        public static Mesh BuildOccluders(Transform root,IReadOnlyList<MeshRenderer> selectedRenderers)
        {
            if(root==null)throw new ArgumentNullException(nameof(root));
            if(Application.isPlaying)throw new InvalidOperationException("Build occluders from authoring meshes in Edit Mode.");
            var vertices=new List<Vector3>();
            var triangles=new List<int>();
            var indicesByPosition=new Dictionary<Vector3,int>();
            int VertexIndex(Vector3 position)
            {
                if(indicesByPosition.TryGetValue(position,out int index))return index;
                index=vertices.Count;vertices.Add(position);indicesByPosition.Add(position,index);
                return index;
            }
            foreach(var renderer in Sources(root,selectedRenderers))
            {
                if(!renderer.enabled || !renderer.TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh==null)continue;
                Mesh source=filter.sharedMesh;
                Vector3[] worldVertices=source.vertices;
                Matrix4x4 matrix=renderer.localToWorldMatrix;
                bool mirrored=matrix.determinant<0;
                for(int i=0;i<worldVertices.Length;i++)worldVertices[i]=matrix.MultiplyPoint3x4(worldVertices[i]);
                for(int sub=0;sub<source.subMeshCount;sub++)
                {
                    int[] indices=source.GetTriangles(sub);
                    for(int t=0;t<indices.Length;t+=3)
                    {
                        Vector3 a=worldVertices[indices[t]],b=worldVertices[indices[t+(mirrored?2:1)]],c=worldVertices[indices[t+(mirrored?1:2)]];
                        if(Vector3.Cross(b-a,c-a).sqrMagnitude<=0)continue;
                        triangles.Add(VertexIndex(a));triangles.Add(VertexIndex(b));triangles.Add(VertexIndex(c));
                    }
                }
            }
            if(triangles.Count==0)throw new InvalidOperationException("No static occluder triangles found.");
            var mesh=new Mesh {name="Occluders",indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
            mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Updates only the generated sun mesh, preserving transport bytes and asset identity.</summary>
        public static void RebuildOccluders(MxSceneRenderingConfiguration configuration)
        {
            if (IsBuilding) throw new InvalidOperationException("Wait for the active transport build to finish.");
            if (configuration == null || configuration.StaticRoot == null || configuration.Data == null ||
                configuration.Data.Occluders == null)
                throw new InvalidOperationException("Existing light transport and static geometry are required.");
            if (Fingerprint(configuration.StaticRoot, configuration.SourceLayout, configuration.StaticSourceRenderers) != configuration.Data.SourceFingerprint)
                throw new InvalidOperationException("Static source changed. Rebuild the complete transport first.");
            Mesh existing = configuration.Data.Occluders;
            string path = AssetDatabase.GetAssetPath(existing);
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) || !path.EndsWith(".asset", StringComparison.Ordinal))
                throw new InvalidOperationException("The occluder mesh must be a generated Assets mesh asset.");
            Mesh compact = BuildOccluders(configuration.StaticRoot, configuration.StaticSourceRenderers);
            try { EditorUtility.CopySerialized(compact, existing); EditorUtility.SetDirty(existing); AssetDatabase.SaveAssetIfDirty(existing); }
            finally { UnityEngine.Object.DestroyImmediate(compact); }
        }

        private sealed class BuildJob : IDisposable
        {
            private const int Resolution=16;
            private static readonly Vector3[] Directions={Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back};
            private static readonly Vector3[] Ups={Vector3.up,Vector3.up,Vector3.back,Vector3.forward,Vector3.up,Vector3.up};
            private readonly Transform _root;
            private readonly IReadOnlyList<MeshRenderer> _selection;
            private readonly string _folder,_fingerprint;
            private readonly float _cell,_spacing;
            private readonly Bounds? _coverage;
            private readonly string _probeSettingsFingerprint;
            private readonly MxSceneLightingProbeQuality _quality;
            private MxSceneLightingProbeLayout _probeLayout;
            private MxSceneLightingProbeGeometry _geometry;
            private bool _dilationPrepared;
            private readonly System.Diagnostics.Stopwatch _elapsed=System.Diagnostics.Stopwatch.StartNew();
            private readonly MxSceneLightingSourceLayout _layout;
            private readonly List<Patch> _patches=new List<Patch>();
            private readonly Dictionary<string,int> _patchKeys=new Dictionary<string,int>();
            private readonly Dictionary<Texture,Texture2D> _textures=new Dictionary<Texture,Texture2D>();
            private readonly Dictionary<Material,Texture2D> _solidTextures=new Dictionary<Material,Texture2D>();
            private Texture2D _solidSurface;
            private readonly List<Vector3> _vertices=new List<Vector3>();
            private readonly List<Color> _ids=new List<Color>();
            private readonly List<int> _triangles=new List<int>();
            private readonly Dictionary<int,Vector4> _weights=new Dictionary<int,Vector4>();
            private readonly List<MxSceneLightingEdge> _edges=new List<MxSceneLightingEdge>();
            private readonly List<MxSceneLightingProbeLink> _probeLinks=new List<MxSceneLightingProbeLink>();
            private MxSceneLightingPayload _payload;
            private Material _material;
            private Mesh _mesh;
            private RenderTexture _capture;
            private Texture2D _readback;
            private readonly CommandBuffer _commands=new CommandBuffer { name="SceneLighting.Hemicube" };
            private readonly MaterialPropertyBlock _properties=new MaterialPropertyBlock();
            private Bounds _bounds;
            private Vector3 _origin;
            private Vector3Int _dimensions;
            private MxSceneLightingProbeGrid _grid;
            private int _surfaceCount,_surfaceCursor,_probeCursor;
            private bool _prepared;
            private float _retainedSum;
            private readonly float[] _momentCounts=new float[96];
            public string Progress => !_prepared?"Preparing static geometry":
                _surfaceCursor<_surfaceCount?"Transport "+_surfaceCursor+"/"+_surfaceCount:"Probes "+_probeCursor+"/"+_payload.Probes.Length;
            public BuildJob(Transform root,string folder,MxSceneLightingSourceLayout layout,
                IReadOnlyList<MeshRenderer> selectedRenderers,float cell,float spacing)
                : this(root,folder,layout,selectedRenderers,cell,spacing,null) { }
            public BuildJob(Transform root,string folder,MxSceneLightingSourceLayout layout,
                IReadOnlyList<MeshRenderer> selectedRenderers,float cell,float spacing,Bounds? coverage)
                : this(root,folder,layout,selectedRenderers,cell,spacing,coverage,null) { }
            public BuildJob(Transform root,string folder,MxSceneLightingSourceLayout layout,
                IReadOnlyList<MeshRenderer> selectedRenderers,float cell,float spacing,Bounds? coverage,MxSceneLightingProbeQuality quality)
            {
                _root=root;_folder=folder.Replace('\\','/').TrimEnd('/');
                if(!_folder.StartsWith("Assets/",StringComparison.Ordinal))throw new ArgumentException("Output must be under Assets.");
                _selection=selectedRenderers!=null && selectedRenderers.Count>0?Sources(root,selectedRenderers):null;
                _layout=layout;_cell=NormalizeSpacing(cell);_spacing=NormalizeSpacing(spacing);
                _fingerprint=Fingerprint(root,layout,_selection);
                _coverage=coverage;
                // Freeze authoring inputs for the asynchronous compiler.
                _quality=quality==null ? null : JsonUtility.FromJson<MxSceneLightingProbeQuality>(JsonUtility.ToJson(quality));
                _probeSettingsFingerprint=QualityFingerprint(coverage,_quality);
            }
            public bool Step(int batch)
            {
                if(!_prepared){Prepare();_prepared=true;return false;}
                for(int i=0;i<batch;i++)
                {
                    if(_surfaceCursor<_surfaceCount)BakeSurface(_surfaceCursor++);
                    else if(_probeCursor<_payload.Probes.Length)BakeProbe(_probeCursor++);
                    else { if(!_dilationPrepared){BuildDilation();_dilationPrepared=true;}return true; }
                }
                return false;
            }
            private void Prepare()
            {
                Shader shader=Shader.Find("Hidden/MxFramework/SceneLightingCapture");
                if(shader==null || !shader.isSupported)throw new InvalidOperationException("Scene lighting capture shader is unavailable.");
                _material=new Material(shader) { hideFlags=HideFlags.HideAndDontSave };
                foreach(var renderer in Sources(_root,_selection))
                {
                    if(!renderer.enabled || !renderer.TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh==null)continue;
                    Mesh mesh=filter.sharedMesh;Vector3[] v=mesh.vertices;Vector2[] uv=mesh.uv;
                    Material[] materials=renderer.sharedMaterials;Matrix4x4 matrix=renderer.localToWorldMatrix;
                    bool mirrored=matrix.determinant<0;
                    for(int sub=0;sub<mesh.subMeshCount;sub++)
                    {
                        Material mat=materials[Math.Min(sub,materials.Length-1)];
                        if(mat==null)throw new InvalidOperationException("Missing static material: "+renderer.name);
                        Texture albedo=mat.HasProperty(_layout.BaseColorProperty)?mat.GetTexture(_layout.BaseColorProperty):null;
                        Texture2D color,surface;
                        if(albedo!=null){color=Readable(albedo);surface=Readable(mat.GetTexture(_layout.SurfaceDataProperty));}
                        else
                        {
                            if(string.IsNullOrEmpty(_layout.SolidColorProperty) || !mat.HasProperty(_layout.SolidColorProperty))
                                throw new InvalidOperationException("No explicit albedo source for "+mat.name);
                            if(!_solidTextures.TryGetValue(mat,out color)){color=new Texture2D(1,1,TextureFormat.RGBAFloat,false,true);color.SetPixel(0,0,SolidColor(mat,_layout));color.Apply();_solidTextures.Add(mat,color);}
                            if(_solidSurface==null){_solidSurface=new Texture2D(1,1,TextureFormat.RGBAFloat,false,true);_solidSurface.SetPixel(0,0,new Color(1,0,0,0.03125f));_solidSurface.Apply();}
                            surface=_solidSurface;
                        }
                        int[] indices=mesh.GetTriangles(sub);
                        float emissionIntensity=EmissionIntensity(mat,_layout);
                        for(int t=0;t<indices.Length;t+=3)
                        {
                            int ia=indices[t],ib=indices[t+(mirrored?2:1)],ic=indices[t+(mirrored?1:2)];
                            AddTriangle(matrix.MultiplyPoint3x4(v[ia]),matrix.MultiplyPoint3x4(v[ib]),matrix.MultiplyPoint3x4(v[ic]),
                                uv.Length>ia?uv[ia]:Vector2.zero,uv.Length>ib?uv[ib]:Vector2.zero,uv.Length>ic?uv[ic]:Vector2.zero,color,surface,emissionIntensity,0);
                        }
                    }
                }
                if(_patches.Count==0)throw new InvalidOperationException("No static surfaces found.");
                _surfaceCount=_patches.Count;
                var coarse=new Dictionary<string,List<int>>();
                for(int i=0;i<_surfaceCount;i++)
                {
                    Patch p=_patches[i];p.Finish();string key=Key(p.Position,p.Normal,_cell*3);
                    if(!coarse.TryGetValue(key,out var children)){children=new List<int>();coarse.Add(key,children);}children.Add(i);
                }
                var nodes=new List<MxSceneLightingNode>();foreach(Patch p in _patches)nodes.Add(p.Node());
                var coarseRows=new List<MxSceneLightingRow>();
                foreach(var children in coarse.Values)
                {
                    int id=nodes.Count;float area=0;Vector3 pos=Vector3.zero,norm=Vector3.zero;
                    foreach(int child in children){Patch p=_patches[child];area+=p.Area;pos+=p.Position*p.Area;norm+=p.Normal*p.Area;}
                    nodes.Add(new MxSceneLightingNode {PositionArea=new Vector4(pos.x/area,pos.y/area,pos.z/area,area),NormalParent=norm.normalized});
                    int start=_edges.Count;
                    foreach(int child in children)
                    {
                        _patches[child].Parent=id;MxSceneLightingNode n=nodes[child];n.NormalParent.w=id;nodes[child]=n;
                        _edges.Add(new MxSceneLightingEdge(child,_patches[child].Area/area));
                    }
                    coarseRows.Add(new MxSceneLightingRow(start,children.Count));
                }
                _mesh=new Mesh {name="SceneLighting.StaticCapture",indexFormat=IndexFormat.UInt32};
                _mesh.SetVertices(_vertices);_mesh.SetColors(_ids);_mesh.SetTriangles(_triangles,0);_mesh.RecalculateBounds();
                var extent=new CaptureExtent(); foreach(var vertex in _vertices)extent.Include(vertex);
                var grid=ProbeGrid(extent.Bounds,_spacing,_coverage);
                if((grid.Count>120000 && _quality?.AutomaticRefinement!=true) || nodes.Count>120000)throw new InvalidOperationException("Scene lighting volume exceeds the first-version build limit.");
                var demand = CreateProbeDemand(_root,_selection,_quality);
                _probeLayout=MxSceneLightingProbeLayout.Create(grid,_quality,demand);
                _grid=_probeLayout.Grid;_bounds=_grid.CaptureBounds;_origin=_grid.Origin;_dimensions=_grid.Dimensions;
                int probes=_probeLayout.Positions.Length;
                _payload=new MxSceneLightingPayload { SurfaceCount=_surfaceCount,Nodes=nodes.ToArray(),Rows=new MxSceneLightingRow[nodes.Count],
                    Probes=new MxSceneLightingProbe[probes],ProbeRows=new MxSceneLightingRow[probes],ProbeVisibility=new Vector2[probes*96] };
                if(_quality!=null && _quality.Enabled)
                {
                    _geometry=new MxSceneLightingProbeGeometry(_vertices);
                    _payload.Version=2;_payload.Topology=_probeLayout.Topology;
                    _payload.ProbeQuality=new Vector4[probes];_payload.ProbeClearDistances=new float[probes*96];
                    _payload.DilationRows=new MxSceneLightingRow[probes];_payload.DilationLinks=Array.Empty<MxSceneLightingEdge>();
                }
                for(int i=0;i<coarseRows.Count;i++)_payload.Rows[_surfaceCount+i]=coarseRows[i];
                _capture=new RenderTexture(Resolution*6,Resolution,24,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear)
                {hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Point};_capture.Create();
                _readback=new Texture2D(Resolution*6,Resolution,TextureFormat.RGBAFloat,false,true) {hideFlags=HideFlags.HideAndDontSave};
            }
            private Texture2D Readable(Texture texture)
            {
                if(texture==null)throw new InvalidOperationException("Missing baked surface texture.");
                if(_textures.TryGetValue(texture,out var copy))return copy;
                var rt=RenderTexture.GetTemporary(texture.width,texture.height,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);
                RenderTexture previous=RenderTexture.active;
                try
                {
                    Graphics.Blit(texture,rt);RenderTexture.active=rt;
                    copy=new Texture2D(texture.width,texture.height,TextureFormat.RGBAFloat,false,true) {hideFlags=HideFlags.HideAndDontSave};
                    copy.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);copy.Apply();_textures.Add(texture,copy);return copy;
                }
                finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);}
            }
            private void AddTriangle(Vector3 a,Vector3 b,Vector3 c,Vector2 ua,Vector2 ub,Vector2 uc,Texture2D color,Texture2D surface,float emissionIntensity,int depth)
            {
                int split=SplitCaptureTriangle(a,b,c,_cell,depth);
                if(split>=0)
                {
                    if(split==0){Vector3 m=(a+b)*0.5f;Vector2 u=(ua+ub)*0.5f;AddTriangle(a,m,c,ua,u,uc,color,surface,emissionIntensity,depth+1);AddTriangle(m,b,c,u,ub,uc,color,surface,emissionIntensity,depth+1);}
                    else if(split==1){Vector3 m=(b+c)*0.5f;Vector2 u=(ub+uc)*0.5f;AddTriangle(a,b,m,ua,ub,u,color,surface,emissionIntensity,depth+1);AddTriangle(a,m,c,ua,u,uc,color,surface,emissionIntensity,depth+1);}
                    else {Vector3 m=(c+a)*0.5f;Vector2 u=(uc+ua)*0.5f;AddTriangle(a,b,m,ua,ub,u,color,surface,emissionIntensity,depth+1);AddTriangle(m,b,c,u,ub,uc,color,surface,emissionIntensity,depth+1);}return;
                }
                if(!CapturableTriangle(a,b,c))return;
                Vector3 cross=Vector3.Cross(b-a,c-a);float area=cross.magnitude*0.5f;
                Vector3 normal=cross.normalized,center=(a+b+c)/3;Vector2 uv=(ua+ub+uc)/3;
                string key=Key(center,normal,_cell);
                if(!_patchKeys.TryGetValue(key,out int id)){id=_patches.Count;_patchKeys.Add(key,id);_patches.Add(new Patch());}
                Color albedo=color.GetPixelBilinear(uv.x,uv.y),data=surface.GetPixelBilinear(uv.x,uv.y);
                int profile=Mathf.Clamp(Mathf.FloorToInt(data.a*16),0,15);
                float diffuse=_layout.DiffuseFractions!=null && _layout.DiffuseFractions.Length==16?Mathf.Clamp01(_layout.DiffuseFractions[profile]):1;
                _patches[id].Add(center,normal,area,albedo,data.b*emissionIntensity,diffuse);
                int first=_vertices.Count;_vertices.Add(a);_vertices.Add(b);_vertices.Add(c);
                Color encoded=new Color(id+1,0,0,1);_ids.Add(encoded);_ids.Add(encoded);_ids.Add(encoded);
                _triangles.Add(first);_triangles.Add(first+1);_triangles.Add(first+2);
            }
            private static string Key(Vector3 p,Vector3 n,float cell)
            { return Mathf.FloorToInt(p.x/cell)+":"+Mathf.FloorToInt(p.y/cell)+":"+Mathf.FloorToInt(p.z/cell)+":"+Mathf.RoundToInt(n.x*3)+":"+Mathf.RoundToInt(n.y*3)+":"+Mathf.RoundToInt(n.z*3); }
            private Color[] Capture(Vector3 position,Quaternion orientation,bool hemisphere)
            {
                int faces=hemisphere?5:6;float far=CaptureFar(position);
                _commands.Clear();_commands.SetRenderTarget(_capture);_commands.ClearRenderTarget(true,true,Color.clear,1);
                for(int face=0;face<faces;face++)
                {
                    Quaternion rotation=orientation*Quaternion.LookRotation(Directions[face],Ups[face]);
                    Matrix4x4 view=Matrix4x4.Scale(new Vector3(1,1,-1))*Matrix4x4.TRS(position,rotation,Vector3.one).inverse;
                    Matrix4x4 vp=GL.GetGPUProjectionMatrix(Matrix4x4.Perspective(90,1,0.025f,far),true)*view;
                    var rect=new Rect(face*Resolution,0,Resolution,Resolution);_commands.SetViewport(rect);
                    _properties.Clear();_properties.SetMatrix("_CaptureVP",vp);_properties.SetMatrix("_CaptureInvVP",vp.inverse);
                    _properties.SetVector("_CaptureOrigin",position);_properties.SetVector("_CaptureViewport",new Vector4(rect.x,rect.y,rect.width,rect.height));
                    _properties.SetVector("_CaptureRange",new Vector4(0.025f,0,far,0));
                    _commands.DrawProcedural(Matrix4x4.identity,_material,1,MeshTopology.Triangles,3,1,_properties);
                    _commands.DrawMesh(_mesh,Matrix4x4.identity,_material,0,0,_properties);
                }
                Graphics.ExecuteCommandBuffer(_commands);RenderTexture previous=RenderTexture.active;
                try{RenderTexture.active=_capture;_readback.ReadPixels(new Rect(0,0,Resolution*6,Resolution),0,0);_readback.Apply();return _readback.GetPixels();}
                finally{RenderTexture.active=previous;}
            }
            private float CaptureFar(Vector3 position)=>Mathf.Max(100,Mathf.Max(_bounds.size.magnitude*1.5f,
                Vector3.Distance(position,_bounds.center)+_bounds.extents.magnitude+_spacing));
            private static Vector3 Decode(Color pixel)
            {
                Vector3 n=new Vector3(pixel.b,pixel.a,1-Mathf.Abs(pixel.b)-Mathf.Abs(pixel.a));
                if(n.z<0){float x=n.x;n.x=(1-Mathf.Abs(n.y))*(x>=0?1:-1);n.y=(1-Mathf.Abs(x))*(n.y>=0?1:-1);}return n.normalized;
            }
            private static float SolidAngle(int x,int y)
            { float u=2*(x+0.5f)/Resolution-1,v=2*(y+0.5f)/Resolution-1;return 4f/(Resolution*Resolution*Mathf.Pow(1+u*u+v*v,1.5f)); }
            private void BakeSurface(int id)
            {
                Patch patch=_patches[id];Quaternion basis=Quaternion.LookRotation(patch.Normal,Mathf.Abs(patch.Normal.y)>0.9f?Vector3.forward:Vector3.up);
                Color[] pixels=Capture(patch.Position+patch.Normal*0.06f,basis,true);_weights.Clear();Vector3 sky=Vector3.zero;float total=0;
                for(int face=0;face<5;face++)for(int y=0;y<Resolution;y++)for(int x=0;x<Resolution;x++)
                {
                    Color p=pixels[y*Resolution*6+face*Resolution+x];Vector3 d=Decode(p);
                    float w=SolidAngle(x,y)*Mathf.Max(0,Vector3.Dot(patch.Normal,d))/Mathf.PI;total+=w;
                    int target=Mathf.RoundToInt(Mathf.Abs(p.r))-1;
                    if(target<0)sky+=SkyWeights(d)*w;
                    else if(target!=id && target<_surfaceCount){_weights.TryGetValue(target,out var old);_weights[target]=old+new Vector4(0,0,0,w);}
                }
                float scale=1/Mathf.Max(1,total);int start=_edges.Count;
                foreach(var pair in Compress(_weights))_edges.Add(new MxSceneLightingEdge(pair.Key,pair.Value.w*scale));
                _payload.Rows[id]=new MxSceneLightingRow(start,_edges.Count-start);
                MxSceneLightingNode node=_payload.Nodes[id];node.Sky=sky*scale;_payload.Nodes[id]=node;
                float before=_weights.Values.Sum(v=>v.w),after=0;for(int i=start;i<_edges.Count;i++)after+=_edges[i].Weight;
                _retainedSum+=before>0.0001f?Mathf.Min(1,after/(before*scale)):1;
            }
            private IEnumerable<KeyValuePair<int,Vector4>> Compress(Dictionary<int,Vector4> values)
            {
                var ordered=values.OrderByDescending(p=>p.Value.w).ThenBy(p=>p.Key).ToArray();
                int fine=Math.Min(24,ordered.Length);for(int i=0;i<fine;i++)yield return ordered[i];
                var distant=new Dictionary<int,Vector4>();
                for(int i=fine;i<ordered.Length;i++)
                {int parent=_patches[ordered[i].Key].Parent;distant.TryGetValue(parent,out var old);distant[parent]=old+ordered[i].Value;}
                foreach(var pair in distant.OrderByDescending(p=>p.Value.w).ThenBy(p=>p.Key).Take(8))yield return pair;
            }
            private void BakeProbe(int id)
            {
                Vector3 position=_probeLayout.Positions[id];Color[] pixels=Capture(position,Quaternion.identity,false);
                float minimumDistance=0;
                if(_payload.Version==2)
                {
                    if(_quality.RelocateNearGeometry)RelocateProbe(id,ref position,ref pixels);
                    _geometry.Nearest(position,CaptureFar(position),out _,out _,out minimumDistance);
                }
                _weights.Clear();Array.Clear(_momentCounts,0,96);Vector4 up=Vector4.zero,equator=Vector4.zero,ground=Vector4.zero;
                float back=0,hits=0,total=0;
                for(int face=0;face<6;face++)for(int py=0;py<Resolution;py++)for(int px=0;px<Resolution;px++)
                {
                    Color p=pixels[py*Resolution*6+face*Resolution+px];Vector3 direction=Decode(p);
                    float w=SolidAngle(px,py)/(4*Mathf.PI);total+=w;Vector4 basis=new Vector4(direction.x,direction.y,direction.z,1)*w;
                    int target=Mathf.RoundToInt(Mathf.Abs(p.r))-1;
                    if(target<0){Vector3 s=SkyWeights(direction);up+=basis*s.x;equator+=basis*s.y;ground+=basis*s.z;}
                    else if(target<_surfaceCount){_weights.TryGetValue(target,out var old);_weights[target]=old+basis;hits+=w;if(p.r<0)back+=w;}
                    int cell=VisibilityCell(direction),index=id*96+cell;float distance=Mathf.Max(0.02f,p.g);
                    _payload.ProbeVisibility[index]+=new Vector2(distance,distance*distance);_momentCounts[cell]++;
                    if(_payload.Version==2)
                    {
                        float old=_payload.ProbeClearDistances[index];
                        _payload.ProbeClearDistances[index]=old==0 ? distance : Mathf.Min(old,distance);
                    }
                }
                for(int k=0;k<96;k++)_payload.ProbeVisibility[id*96+k]/=Mathf.Max(1,_momentCounts[k]);
                float inv=1/Mathf.Max(total,0.001f);int start=_probeLinks.Count;
                foreach(var pair in Compress(_weights))_probeLinks.Add(new MxSceneLightingProbeLink {Target=pair.Key,DirectionWeight=pair.Value*inv});
                _payload.ProbeRows[id]=new MxSceneLightingRow(start,_probeLinks.Count-start);
                bool valid=back*inv<=.55f && (_payload.Version==1 || minimumDistance+0.0001f>=_quality.MinimumClearance);
                _payload.Probes[id]=new MxSceneLightingProbe {PositionValid=new Vector4(position.x,position.y,position.z,valid?1:0),SkyUp=up*inv,SkyEquator=equator*inv,SkyGround=ground*inv};
                if(_payload.Version==2)_payload.ProbeQuality[id]=new Vector4(Vector3.Distance(position,_probeLayout.Positions[id]),minimumDistance,back*inv,0);
            }

            private static float BackFraction(Color[] pixels)
            {
                float back=0,total=0;
                for(int face=0;face<6;face++)for(int y=0;y<Resolution;y++)for(int x=0;x<Resolution;x++)
                {float weight=SolidAngle(x,y);total+=weight;if(pixels[y*Resolution*6+face*Resolution+x].r<0)back+=weight;}
                return back/Mathf.Max(total,.0001f);
            }
            private void RelocateProbe(int id,ref Vector3 position,ref Color[] pixels)
            {
                float limit=_probeLayout.LocalSpacing[id]*_quality.MaximumRelocationFraction;
                if(limit<=0 || !_geometry.Nearest(position,limit+_quality.MinimumClearance,out var closest,out var normal,out float distance))return;
                bool inside=BackFraction(pixels)>.55f;
                if(!inside && distance>=_quality.MinimumClearance)return;
                Vector3 nominal=position;
                Vector3 away=distance>.00001f ? (position-closest)/distance : normal;
                // Outward triangle normals are tried first for interior points, then the opposite
                // side. Every accepted candidate has fresh capture and exact triangle clearance.
                var directions=inside ? new[]{normal,-normal,away,-away} : new[]{away,normal,-normal};
                foreach(Vector3 direction in directions)
                {
                    Vector3 candidate=closest+direction*(_quality.MinimumClearance+.002f);
                    if(Vector3.Distance(candidate,nominal)>limit)continue;
                    if(_geometry.Nearest(candidate,_quality.MinimumClearance,out _,out _,out _))continue;
                    if(!inside && !_geometry.SegmentClear(nominal,candidate))continue;
                    Color[] capture=Capture(candidate,Quaternion.identity,false);
                    if(BackFraction(capture)>.55f)continue;
                    position=candidate;pixels=capture;return;
                }
            }

            private void BuildDilation()
            {
                if(_payload.Version!=2 || !_quality.VisibilityConstrainedDilation)return;
                var buckets=new Dictionary<Vector3Int,List<int>>();
                Vector3Int Bucket(Vector3 p)=>new Vector3Int(Mathf.FloorToInt(p.x/_grid.Spacing),Mathf.FloorToInt(p.y/_grid.Spacing),Mathf.FloorToInt(p.z/_grid.Spacing));
                for(int id=0;id<_payload.Probes.Length;id++)if(_payload.Probes[id].PositionValid.w==1)
                {var key=Bucket(_payload.Probes[id].PositionValid);if(!buckets.TryGetValue(key,out var list)){list=new List<int>();buckets.Add(key,list);}list.Add(id);}
                var links=new List<MxSceneLightingEdge>();var candidates=new List<MxSceneLightingEdge>();
                for(int id=0;id<_payload.Probes.Length;id++)
                {
                    if(_payload.Probes[id].PositionValid.w!=0 || _payload.ProbeQuality[id].z>.55f || _payload.ProbeQuality[id].y<.025f)continue;
                    Vector3 position=_payload.Probes[id].PositionValid;var bucket=Bucket(position);candidates.Clear();
                    float radius=_probeLayout.LocalSpacing[id]*1.75f;
                    for(int z=-2;z<=2;z++)for(int y=-2;y<=2;y++)for(int x=-2;x<=2;x++)
                    {
                        if(!buckets.TryGetValue(bucket+new Vector3Int(x,y,z),out var ids))continue;
                        foreach(int source in ids)
                        {
                            Vector3 target=_payload.Probes[source].PositionValid,delta=target-position;float distance=delta.magnitude;
                            if(distance>.0001f && distance<=radius && VisibleDilationSegment(_payload,id,source,_geometry))
                                candidates.Add(new MxSceneLightingEdge(source,1/Mathf.Max(.01f,distance*distance)));
                        }
                    }
                    candidates.Sort((a,b)=>b.Weight.CompareTo(a.Weight));int count=Math.Min(6,candidates.Count);float total=0;
                    for(int j=0;j<count;j++)total+=candidates[j].Weight;
                    int start=links.Count;for(int j=0;j<count;j++)links.Add(new MxSceneLightingEdge(candidates[j].Target,candidates[j].Weight/total));
                    _payload.DilationRows[id]=new MxSceneLightingRow(start,count);
                    if(count>0){var p=_payload.Probes[id];p.PositionValid.w=2;_payload.Probes[id]=p;var q=_payload.ProbeQuality[id];q.w=1;_payload.ProbeQuality[id]=q;}
                }
                _payload.DilationLinks=links.ToArray();
            }
            private static Vector3 SkyWeights(Vector3 d)=>new Vector3(Mathf.Max(0,d.y),1-Mathf.Abs(d.y),Mathf.Max(0,-d.y));
            private static int VisibilityCell(Vector3 d)
            {
                Vector3 a=new Vector3(Mathf.Abs(d.x),Mathf.Abs(d.y),Mathf.Abs(d.z));int face;Vector2 uv;
                if(a.x>=a.y && a.x>=a.z){face=d.x>=0?0:1;uv=new Vector2(d.x>=0?-d.z:d.z,d.y)/Mathf.Max(a.x,0.00001f);}
                else if(a.y>=a.z){face=d.y>=0?2:3;uv=new Vector2(d.x,d.y>=0?-d.z:d.z)/Mathf.Max(a.y,0.00001f);}
                else{face=d.z>=0?4:5;uv=new Vector2(d.z>=0?d.x:-d.x,d.y)/Mathf.Max(a.z,0.00001f);}
                int x=Mathf.Clamp((int)((uv.x*0.5f+0.5f)*4),0,3),y=Mathf.Clamp((int)((uv.y*0.5f+0.5f)*4),0,3);return face*16+y*4+x;
            }
            public MxSceneLightingData Save()
            {
                if(Fingerprint(_root,_layout,_selection)!=_fingerprint)throw new InvalidOperationException("Static source changed during the lighting build; result was not adopted.");
                _payload.Edges=_edges.ToArray();_payload.ProbeLinks=_probeLinks.ToArray();byte[] bytes=_payload.Write();
                Directory.CreateDirectory(_folder);
                File.WriteAllBytes(_folder+"/Transport.bytes",bytes);AssetDatabase.ImportAsset(_folder+"/Transport.bytes",ImportAssetOptions.ForceSynchronousImport);
                string meshPath=_folder+"/Occluders.asset";Mesh existing=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                Mesh occluders=BuildOccluders(_root,_selection);
                try
                {
                    if(existing==null){AssetDatabase.CreateAsset(occluders,meshPath);existing=occluders;}
                    else{EditorUtility.CopySerialized(occluders,existing);EditorUtility.SetDirty(existing);}
                }
                finally {if(!AssetDatabase.Contains(occluders))UnityEngine.Object.DestroyImmediate(occluders);}
                string dataPath=_folder+"/SceneLighting.asset";var data=AssetDatabase.LoadAssetAtPath<MxSceneLightingData>(dataPath);
                if(data==null){data=ScriptableObject.CreateInstance<MxSceneLightingData>();AssetDatabase.CreateAsset(data,dataPath);}
                data.Initialize(AssetDatabase.LoadAssetAtPath<TextAsset>(_folder+"/Transport.bytes"),existing,_fingerprint,_bounds,_origin,_dimensions,_grid.Spacing,
                    _surfaceCount,_payload.Nodes.Length,_payload.Probes.Length,_retainedSum/_surfaceCount,_probeSettingsFingerprint,_spacing);
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssetIfDirty(existing);
                AssetDatabase.SaveAssetIfDirty(data);
                Debug.Log($"SceneLighting probe build metrics: {_elapsed.Elapsed.TotalSeconds:F3}s; payload {bytes.LongLength} bytes; probes {_payload.Probes.Length}; bricks {_payload.Topology?.BrickCount??0}.");
                return data;
            }
            public void Dispose()
            {
                _commands.Dispose();UnityEngine.Object.DestroyImmediate(_material);UnityEngine.Object.DestroyImmediate(_mesh);
                if(_capture!=null)_capture.Release();UnityEngine.Object.DestroyImmediate(_capture);UnityEngine.Object.DestroyImmediate(_readback);
                foreach(var texture in _textures.Values)UnityEngine.Object.DestroyImmediate(texture);
                foreach(var texture in _solidTextures.Values)UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(_solidSurface);
            }
            private sealed class Patch
            {
                public Vector3 Position,Normal,Reflectance,Emission;public float Area;public int Parent;
                public void Add(Vector3 p,Vector3 n,float area,Color color,float emission,float diffuse)
                {Position+=p*area;Normal+=n*area;Reflectance+=new Vector3(color.r,color.g,color.b)*(area*diffuse);Emission+=new Vector3(color.r,color.g,color.b)*emission*area;Area+=area;}
                public void Finish(){Position/=Area;Normal.Normalize();Reflectance/=Area;Emission/=Area;Reflectance=Vector3.Min(Reflectance,Vector3.one*0.95f);}
                public MxSceneLightingNode Node()=>new MxSceneLightingNode {PositionArea=new Vector4(Position.x,Position.y,Position.z,Area),NormalParent=Normal,Albedo=Reflectance,Emission=Emission};
            }
        }
    }
}


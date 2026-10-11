using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using UnityCamera = UnityEngine.Camera;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Spatial visibility and draw packing are derived independently from scene authority.</summary>
    internal sealed class CharacterTestVegetationRenderer
    {
        internal const int FallbackBatchCapacity = 256;
        internal const string IndirectKeyword = "WG_FOLIAGE_INDIRECT";
        private static readonly int InstancesId = Shader.PropertyToID("_WgVegetationInstances");
        private static readonly int VisibleIndicesId = Shader.PropertyToID("_WgVegetationVisibleIndices");
        private static readonly int UseVisibleIndicesId = Shader.PropertyToID("_WgVegetationUseVisibleIndices");

        [StructLayout(LayoutKind.Sequential)]
        internal struct GpuInstance
        {
            internal Matrix4x4 ObjectToWorld;
            internal Matrix4x4 WorldToObject;
        }

        internal sealed class Batch
        {
            internal Vector2Int Cell;
            internal CharacterTestVegetationType Type;
            internal Mesh Mesh;
            internal Material Material;
            internal Matrix4x4[] Matrices;
            internal Matrix4x4[] InverseMatrices;
            internal Bounds Bounds;
            internal int Index;
            internal Mesh[] LodMeshes;
            internal Vector3[] Centers;
            internal float[] Radii;
            internal Vector3[] InstanceExtents;
        }
        private sealed class DrawGroup
        {
            internal Mesh Mesh;
            internal Material Material;
            internal readonly List<Batch> Cells = new List<Batch>();
        }

        /// <summary>One actual draw, with private buffers so cameras and sibling draws cannot alias.</summary>
        internal sealed class DrawBatch : IDisposable
        {
            internal Mesh Mesh;
            internal Material Material;
            internal int Count;
            internal Bounds Bounds;
            internal bool Indirect;
            internal GpuInstance[] Instances;
            internal Matrix4x4[] Matrices;
            internal int Lod;
            internal int UploadedInstances { get; private set; }
            private long[] _identities;
            private bool _dataDirty;
            private int _uploadedCount = -1;
            private Mesh _uploadedMesh;
            private GraphicsBuffer _instances, _arguments;
            private readonly GraphicsBuffer.IndirectDrawIndexedArgs[] _args = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
            private readonly MaterialPropertyBlock _properties = new MaterialPropertyBlock();
            internal long GpuBytes => _instances == null ? 0 : (long)_instances.count * 128 + GraphicsBuffer.IndirectDrawIndexedArgs.size;
            internal void Reset(Mesh mesh, Material material, int capacity, bool indirect)
            {
                bool formatChanged = Indirect != indirect;
                Mesh = mesh; Material = material; Count = 0; Bounds = default; Indirect = indirect;
                if (formatChanged || _identities == null || _identities.Length != capacity)
                { _identities = new long[capacity]; _dataDirty = true; }
                if (indirect)
                {
                    if (Instances == null || Instances.Length != capacity)
                    { Dispose(); Instances = new GpuInstance[capacity]; }
                    Matrices = null;
                }
                else
                {
                    Dispose(); Instances = null;
                    if (Matrices == null || Matrices.Length != capacity) Matrices = new Matrix4x4[capacity];
                }
            }
            internal void Append(Batch cell, int offset, int count)
            {
                if (Count == 0) Bounds = cell.Bounds; else Bounds.Encapsulate(cell.Bounds);
                for (int i = 0; i < count; i++)
                {
                    long identity = ((long)(cell.Index + 1) << 32) | (uint)(offset + i);
                    int destination = Count + i;
                    if (_identities[destination] == identity) continue;
                    _identities[destination] = identity; _dataDirty = true;
                    if (Indirect) Instances[destination] = new GpuInstance
                    { ObjectToWorld = cell.Matrices[offset + i], WorldToObject = cell.InverseMatrices[offset + i] };
                    else Matrices[destination] = cell.Matrices[offset + i];
                }
                Count += count;
            }
            internal void Submit(UnityCamera camera, int layer, Material indirectMaterial)
            {
                UploadedInstances = 0;
                var parameters = new RenderParams(Indirect ? indirectMaterial : Material)
                {
                    camera = camera, layer = layer, worldBounds = Bounds,
                    shadowCastingMode = ShadowCastingMode.Off, receiveShadows = true,
                    motionVectorMode = MotionVectorGenerationMode.ForceNoMotion,
                    lightProbeUsage = LightProbeUsage.Off, reflectionProbeUsage = ReflectionProbeUsage.Off
                };
                if (!Indirect)
                { Graphics.RenderMeshInstanced(parameters, Mesh, 0, Matrices, Count); return; }
                if (_instances == null)
                {
                    _instances = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Instances.Length, 128);
                    try { _arguments = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size); }
                    catch { _instances.Dispose(); _instances = null; throw; }
                    _properties.SetBuffer(InstancesId, _instances);
                    _properties.SetBuffer(VisibleIndicesId, _instances); // Bound but never read by the CPU-packed variant.
                    _properties.SetInteger(UseVisibleIndicesId, 0);
                }
                if (_dataDirty || _uploadedCount != Count)
                { _instances.SetData(Instances, 0, 0, Count); UploadedInstances = Count; }
                _args[0] = new GraphicsBuffer.IndirectDrawIndexedArgs
                {
                    indexCountPerInstance = Mesh.GetIndexCount(0), instanceCount = (uint)Count,
                    startIndex = Mesh.GetIndexStart(0), baseVertexIndex = (uint)Mesh.GetBaseVertex(0), startInstance = 0
                };
                if (_dataDirty || _uploadedCount != Count || _uploadedMesh != Mesh) _arguments.SetData(_args);
                _uploadedCount = Count; _uploadedMesh = Mesh;
                _dataDirty = false;
                parameters.matProps = _properties;
                Graphics.RenderMeshIndirect(parameters, Mesh, _arguments, 1);
            }
            public void Dispose()
            { _instances?.Dispose(); _arguments?.Dispose(); _instances = null; _arguments = null; _uploadedCount = -1; _uploadedMesh = null; _properties.Clear(); }
        }
        private sealed class CameraDraws : IDisposable
        {
            internal readonly List<DrawBatch> Active = new List<DrawBatch>();
            internal readonly Dictionary<(Mesh mesh, Material material, int lod), DrawBatch> Open =
                new Dictionary<(Mesh, Material, int), DrawBatch>();
            private readonly List<DrawBatch> _pool = new List<DrawBatch>();
            internal void Begin() { Active.Clear(); Open.Clear(); }
            internal DrawBatch Add(Mesh mesh, Material material, int capacity, bool indirect)
            {
                int i = Active.Count;
                if (i == _pool.Count) _pool.Add(new DrawBatch());
                var draw = _pool[i]; draw.Reset(mesh, material, capacity, indirect); Active.Add(draw); return draw;
            }
            internal long GpuBytes { get { long bytes = 0; foreach (var draw in _pool) bytes += draw.GpuBytes; return bytes; } }
            public void Dispose() { foreach (var draw in _pool) draw.Dispose(); _pool.Clear(); Active.Clear(); }
        }
        private sealed class CameraState : IDisposable
        {
            private readonly List<CameraDraws> _invocations = new List<CameraDraws>();
            internal int Depth;
            internal byte[][] Tiers;
            internal CameraDraws Get(int index)
            { while (_invocations.Count <= index) _invocations.Add(new CameraDraws()); return _invocations[index]; }
            internal long GpuBytes { get { long bytes = 0; foreach (var view in _invocations) bytes += view.GpuBytes; return bytes; } }
            public void Dispose() { foreach (var view in _invocations) view.Dispose(); _invocations.Clear(); Depth = 0; }
        }

        private readonly CharacterTestVegetationSource _owner;
        private readonly List<Batch> _batches = new List<Batch>();
        private readonly List<DrawGroup> _groups = new List<DrawGroup>();
        private readonly Dictionary<UnityCamera, CameraState> _cameras = new Dictionary<UnityCamera, CameraState>();
        private readonly List<UnityCamera> _deadCameras = new List<UnityCamera>();
        private readonly Dictionary<Material, Material> _indirectMaterials = new Dictionary<Material, Material>();
        private readonly Plane[] _planes = new Plane[6];
        private Matrix4x4 _worldMatrix;
        private bool _dirty = true;
        private bool _hasAuthoredLod, _gpuAttempted;
        private CharacterTestVegetationGpuClassification _gpu;
        private string _gpuIssue = string.Empty;
        private string _layoutIssue = string.Empty;
        private string _fallbackIssue = string.Empty;
        internal IReadOnlyList<Batch> Batches { get { EnsureBuilt(); return _batches; } }
        internal int ChunkCount { get; private set; }
        internal int VisibleInstances { get; private set; }
        internal readonly int[] LodInstances = new int[3];
        internal long SubmittedTriangles { get; private set; }
        internal int UploadedInstances { get; private set; }
        internal long LodEvaluations { get; private set; }
        internal long LodTransitions { get; private set; }
        internal int SubmittedBatches { get; private set; }
        internal int EffectiveBatchCapacity { get; private set; }
        internal bool UsingIndirect { get; private set; }
        internal bool UsingGpuClassification { get; private set; }
        internal bool CountsAvailable => !UsingGpuClassification;
        internal CharacterTestVegetationGpuClassification GpuClassification => _gpu;
        internal double CpuPreparationMilliseconds { get; private set; }
        internal UnityCamera LastCamera { get; private set; }
        internal double RebuildMilliseconds { get; private set; }
        internal string Issue { get; private set; } = string.Empty;
        internal long ResidentGpuBytes { get { long bytes = _gpu == null ? 0 : _gpu.ResidentBytes; foreach (var camera in _cameras.Values) bytes += camera.GpuBytes; return bytes; } }
        internal CharacterTestVegetationRenderer(CharacterTestVegetationSource owner) { _owner = owner; }
        internal void Disable()
        {
            ReleaseResources(); _batches.Clear(); _groups.Clear(); _dirty = true;
            SubmittedBatches = VisibleInstances = EffectiveBatchCapacity = 0; UsingIndirect = false; LastCamera = null;
            UsingGpuClassification = false;
        }
        internal void Invalidate() => _dirty = true;
        internal void EndCamera(UnityCamera camera)
        {
            if (camera != null && _cameras.TryGetValue(camera, out var state)) state.Depth = Math.Max(0, state.Depth - 1);
            _gpu?.End(camera);
        }
        private CameraState GetCamera(UnityCamera camera)
        {
            _deadCameras.Clear();
            foreach (var pair in _cameras) if (pair.Key == null) _deadCameras.Add(pair.Key);
            foreach (var dead in _deadCameras) { _cameras[dead].Dispose(); _cameras.Remove(dead); }
            if (!_cameras.TryGetValue(camera, out var state)) _cameras.Add(camera, state = new CameraState());
            return state;
        }

        internal void Submit(UnityCamera camera)
        {
            if (_owner == null || !_owner.isActiveAndEnabled || camera == null || camera.cameraType == CameraType.Preview ||
                (camera.cullingMask & (1 << _owner.gameObject.layer)) == 0) return;
            EnsureBuilt(); Issue = _layoutIssue; SubmittedBatches = VisibleInstances = EffectiveBatchCapacity = 0; UsingIndirect = false; UsingGpuClassification = false; LastCamera = camera;
            UploadedInstances = 0; SubmittedTriangles = 0; Array.Clear(LodInstances, 0, LodInstances.Length);
            if (!SystemInfo.supportsInstancing) { Issue = "设备不支持实例化，植被未提交。"; return; }
            bool indirect = SystemInfo.supportsComputeShaders && SystemInfo.supportsIndirectArgumentsBuffer &&
                SystemInfo.graphicsShaderLevel >= 45 && !camera.stereoEnabled;
            foreach (var group in _groups)
                indirect &= group.Material.shader.keywordSpace.FindKeyword(IndirectKeyword).isValid;
            UsingIndirect = indirect; EffectiveBatchCapacity = indirect ? _owner.DrawBatchSize : Math.Min(_owner.DrawBatchSize, FallbackBatchCapacity);
            bool requestedGpu = _owner.ClassificationMode == CharacterTestVegetationClassificationMode.GpuInstances
                || (_owner.ClassificationMode == CharacterTestVegetationClassificationMode.Auto && _hasAuthoredLod);
            if (requestedGpu && indirect && _batches.Count > 0)
            {
                if (!_gpuAttempted)
                {
                    _gpuAttempted = true;
                    CharacterTestVegetationGpuClassification.TryCreate(_batches, _owner.DrawBatchSize, out _gpu, out _gpuIssue);
                }
                if (_gpu != null)
                {
                    long started = Stopwatch.GetTimestamp();
                    _gpu.Submit(camera, _owner.gameObject.layer);
                    CpuPreparationMilliseconds = (Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency;
                    UsingGpuClassification = true; SubmittedBatches = _gpu.PacketCount; UploadedInstances = _gpu.LastUploadedInstances;
                    // Rendering never reads GPU results synchronously. Query an explicitly versioned async snapshot instead.
                    VisibleInstances = -1; SubmittedTriangles = -1;
                    for (int tier = 0; tier < 3; tier++) LodInstances[tier] = -1;
                    return;
                }
            }
            var state = GetCamera(camera); var view = state.Get(state.Depth++);
            long cpuStarted = Stopwatch.GetTimestamp();
            Fill(camera, indirect, view, state, _owner.ClassificationMode == CharacterTestVegetationClassificationMode.CpuInstanceReference);
            foreach (var draw in view.Active)
            {
                if (!draw.Material.enableInstancing) { Issue = "草材质未启用实例化，相关草未提交。"; continue; }
                Material material = null;
                if (indirect)
                {
                    if (!_indirectMaterials.TryGetValue(draw.Material, out material))
                    {
                        material = new Material(draw.Material) { name = draw.Material.name + " (Vegetation Indirect)", hideFlags = HideFlags.HideAndDontSave };
                        _indirectMaterials.Add(draw.Material, material);
                    }
                    // Keep authoring material edits live without modifying the shared asset.
                    material.CopyPropertiesFromMaterial(draw.Material); material.EnableKeyword(IndirectKeyword); material.enableInstancing = true;
                }
                draw.Submit(camera, _owner.gameObject.layer, material);
                SubmittedBatches++; VisibleInstances += draw.Count;
                LodInstances[draw.Lod] += draw.Count; UploadedInstances += draw.UploadedInstances;
                SubmittedTriangles += (long)(draw.Mesh.GetIndexCount(0) / 3) * draw.Count;
            }
            CpuPreparationMilliseconds = (Stopwatch.GetTimestamp() - cpuStarted) * 1000d / Stopwatch.Frequency;
            if (string.IsNullOrEmpty(Issue) && !indirect) Issue = _fallbackIssue;
            if (string.IsNullOrEmpty(Issue) && requestedGpu && !string.IsNullOrEmpty(_gpuIssue)) Issue = _gpuIssue;
        }

        /// <summary>CPU preparation only, also permits explicit fallback acceptance without drawing.</summary>
        internal IReadOnlyList<DrawBatch> Prepare(UnityCamera camera, bool indirect)
        {
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            EnsureBuilt(); var state = GetCamera(camera); var view = state.Get(0); Fill(camera, indirect, view, state, false); return view.Active;
        }
        /// <summary>CPU reference for the same per-instance wind-expanded bounds as the GPU path.</summary>
        internal IReadOnlyList<DrawBatch> PrepareInstanceReference(UnityCamera camera, bool indirect)
        {
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            EnsureBuilt(); var state = GetCamera(camera); var view = state.Get(0); Fill(camera, indirect, view, state, true); return view.Active;
        }
        private void Fill(UnityCamera camera, bool indirect, CameraDraws view, CameraState state, bool perInstanceVisibility)
        {
            view.Begin(); GeometryUtility.CalculateFrustumPlanes(camera, _planes);
            if (state.Tiers == null)
            {
                state.Tiers = new byte[_batches.Count][];
                for (int b = 0; b < _batches.Count; b++)
                { state.Tiers[b] = new byte[_batches[b].Matrices.Length]; for (int i = 0; i < state.Tiers[b].Length; i++) state.Tiers[b][i] = 255; }
            }
            Matrix4x4 cameraView = camera.worldToCameraMatrix, projection = camera.projectionMatrix;
            Vector3 position = camera.transform.position;
            int limit = indirect ? _owner.DrawBatchSize : Math.Min(_owner.DrawBatchSize, FallbackBatchCapacity);
            int capacity = Math.Max(1, Math.Min(limit, _owner.Instances.Count));
            foreach (var group in _groups)
            {
                foreach (var cell in group.Cells)
                {
                    if (!GeometryUtility.TestPlanesAABB(_planes, cell.Bounds) ||
                        cell.Bounds.SqrDistance(position) > cell.Type.DrawDistance * cell.Type.DrawDistance) continue;
                    byte[] tiers = state.Tiers[cell.Index];
                    for (int offset = 0; offset < cell.Matrices.Length; offset++)
                    {
                        if (perInstanceVisibility)
                        {
                            var bounds = new Bounds(cell.Centers[offset], cell.InstanceExtents[offset] * 2);
                            if (!GeometryUtility.TestPlanesAABB(_planes, bounds)
                                || bounds.SqrDistance(position) > cell.Type.DrawDistance * cell.Type.DrawDistance) continue;
                        }
                        int tier = 0;
                        if (cell.LodMeshes[1] != null && cell.LodMeshes[2] != null)
                        {
                            float height = PresentationGeometryLodPolicy.ScreenHeight(cameraView, projection,
                                cell.Centers[offset], cell.Radii[offset], camera.orthographic);
                            tier = PresentationGeometryLodPolicy.Select(height, cell.Type.Lod0ScreenHeight,
                                cell.Type.Lod1ScreenHeight, tiers[offset], cell.Type.LodHysteresis); LodEvaluations++;
                            if (tiers[offset] != 255 && tiers[offset] != tier) LodTransitions++;
                        }
                        tiers[offset] = (byte)tier;
                        Mesh mesh = cell.LodMeshes[tier] ?? cell.Mesh;
                        var key = (mesh, group.Material, tier);
                        if (!view.Open.TryGetValue(key, out DrawBatch draw) || draw.Count == capacity)
                        { draw = view.Add(mesh, group.Material, capacity, indirect); draw.Lod = tier; view.Open[key] = draw; }
                        draw.Append(cell, offset, 1);
                    }
                }
            }
        }

        internal void EnsureBuilt()
        {
            if (_owner == null) return;
            Matrix4x4 world = _owner.transform.localToWorldMatrix;
            if (!_dirty && world == _worldMatrix) return;
            long start = Stopwatch.GetTimestamp();
            ReleaseResources(); _batches.Clear(); _groups.Clear(); ChunkCount = 0; _hasAuthoredLod = false; Issue = _layoutIssue = string.Empty;
            _worldMatrix = world; _dirty = false;
            _fallbackIssue = "实际为实例化回退，单次容量 " + Math.Min(_owner.DrawBatchSize, FallbackBatchCapacity) + "；配置 " + _owner.DrawBatchSize + " 仅用于 Indirect。";
            if (!_owner.TryValidate(out string issue)) { Issue = _layoutIssue = issue; return; }
            var types = new Dictionary<int, CharacterTestVegetationType>();
            foreach (var type in _owner.Types) types.Add(type.Id, type);
            var cells = new SortedDictionary<(int x, int z, int type), List<Matrix4x4>>();
            var cellIds = new HashSet<Vector2Int>();
            foreach (var item in _owner.Instances)
            {
                int x = Mathf.FloorToInt(item.Position.x / _owner.ChunkSize), z = Mathf.FloorToInt(item.Position.z / _owner.ChunkSize);
                var key = (x, z, item.TypeId);
                if (!cells.TryGetValue(key, out var matrices)) cells.Add(key, matrices = new List<Matrix4x4>());
                matrices.Add(world * Matrix4x4.TRS(item.Position, item.Rotation.normalized, item.Scale)); cellIds.Add(new Vector2Int(x, z));
            }
            var groups = new Dictionary<(Mesh mesh, Material material), DrawGroup>();
            foreach (var cell in cells)
            {
                var type = types[cell.Key.type]; Mesh mesh = type.Prefab.GetComponent<MeshFilter>().sharedMesh;
                Material material = type.Prefab.GetComponent<MeshRenderer>().sharedMaterial;
                bool lodValid = type.Lod1Mesh != null && type.Lod2Mesh != null && type.Lod1Mesh != mesh &&
                    type.Lod2Mesh != mesh && type.Lod1Mesh != type.Lod2Mesh &&
                    type.Lod1Mesh.subMeshCount == 1 && type.Lod2Mesh.subMeshCount == 1 &&
                    type.Lod1Mesh.GetIndexCount(0) < mesh.GetIndexCount(0) &&
                    type.Lod2Mesh.GetIndexCount(0) < type.Lod1Mesh.GetIndexCount(0);
                var levels = new[] { mesh, lodValid ? type.Lod1Mesh : null, lodValid ? type.Lod2Mesh : null };
                _hasAuthoredLod |= lodValid;
                Bounds localBounds = mesh.bounds;
                if (lodValid) { localBounds.Encapsulate(type.Lod1Mesh.bounds); localBounds.Encapsulate(type.Lod2Mesh.bounds); }
                var matrices = cell.Value.ToArray(); var inverses = new Matrix4x4[matrices.Length];
                var centers = new Vector3[matrices.Length]; var radii = new float[matrices.Length]; var instanceExtents = new Vector3[matrices.Length];
                Bounds bounds = TransformBounds(localBounds, matrices[0]);
                for (int i = 0; i < matrices.Length; i++)
                {
                    inverses[i] = matrices[i].inverse; Bounds instanceBounds = TransformBounds(localBounds, matrices[i]);
                    centers[i] = instanceBounds.center; radii[i] = instanceBounds.extents.magnitude;
                    instanceExtents[i] = instanceBounds.extents + Vector3.one;
                    if (i > 0) bounds.Encapsulate(instanceBounds);
                }
                // Expand takes full size: 2 m adds 1 m per side. Foliage wind is
                // bounded to < 0.427 world metres, even at maximum material/global
                // strength and under nonuniform owner/instance scales. Keep the
                // same conservative bound for cell culling and submitted draws.
                bounds.Expand(2f);
                var batch = new Batch { Cell = new Vector2Int(cell.Key.x, cell.Key.z), Type = type, Mesh = mesh, Material = material,
                    Matrices = matrices, InverseMatrices = inverses, Bounds = bounds, Index = _batches.Count,
                    LodMeshes = levels, Centers = centers, Radii = radii, InstanceExtents = instanceExtents };
                _batches.Add(batch);
                var key = (mesh, material);
                if (!groups.TryGetValue(key, out var group))
                { group = new DrawGroup { Mesh = mesh, Material = material }; groups.Add(key, group); _groups.Add(group); }
                group.Cells.Add(batch);
            }
            ChunkCount = cellIds.Count;
            RebuildMilliseconds = (Stopwatch.GetTimestamp() - start) * 1000d / Stopwatch.Frequency;
        }
        private void ReleaseResources()
        {
            _gpu?.Dispose(); _gpu = null; _gpuAttempted = false; _gpuIssue = string.Empty;
            foreach (var state in _cameras.Values) state.Dispose(); _cameras.Clear(); _deadCameras.Clear();
            foreach (var material in _indirectMaterials.Values)
                if (material != null) { if (Application.isPlaying) UnityEngine.Object.Destroy(material); else UnityEngine.Object.DestroyImmediate(material); }
            _indirectMaterials.Clear();
        }
        private static Bounds TransformBounds(Bounds local, Matrix4x4 matrix)
        {
            Vector3 a = matrix.MultiplyVector(new Vector3(local.extents.x, 0, 0));
            Vector3 b = matrix.MultiplyVector(new Vector3(0, local.extents.y, 0));
            Vector3 c = matrix.MultiplyVector(new Vector3(0, 0, local.extents.z));
            return new Bounds(matrix.MultiplyPoint3x4(local.center), 2 * new Vector3(Mathf.Abs(a.x)+Mathf.Abs(b.x)+Mathf.Abs(c.x),
                Mathf.Abs(a.y)+Mathf.Abs(b.y)+Mathf.Abs(c.y), Mathf.Abs(a.z)+Mathf.Abs(b.z)+Mathf.Abs(c.z)));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Profiling;
using UnityCamera = UnityEngine.Camera;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Shared immutable records, camera-owned history and invocation-owned indirect output.</summary>
    internal sealed class CharacterTestVegetationGpuClassification : IDisposable
    {
        internal const string ResourcePath = "WGame/Vegetation/VegetationClassification";
        [StructLayout(LayoutKind.Sequential)]
        private struct Record
        {
            internal Vector4 CenterRadius, ExtentsDistance, Policy;
            internal uint Group0, Group1, Group2, HasLod;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct UInt4
        {
            internal uint X, Y, Z, W;
            internal UInt4(uint x, uint y, uint z) { X = x; Y = y; Z = z; W = 0; }
        }
        private sealed class Group
        {
            internal Mesh Mesh;
            internal Material Material;
            internal Bounds Bounds;
            internal int Tier, Population, VisibleOffset;
        }
        private readonly struct Packet
        {
            internal readonly int Group, Offset, Capacity;
            internal Packet(int group, int offset, int capacity) { Group = group; Offset = offset; Capacity = capacity; }
        }
        private sealed class Invocation : IDisposable
        {
            internal GraphicsBuffer Visible, Arguments, Counts;
            internal MaterialPropertyBlock[] Properties;
            internal Matrix4x4 View, Projection;
            internal Vector3 Position;
            internal bool Ready, Orthographic;
            internal long Revision;
            internal long Bytes => (Visible == null ? 0 : (long)Visible.count * Visible.stride)
                + (Arguments == null ? 0 : (long)Arguments.count * Arguments.stride)
                + (Counts == null ? 0 : (long)Counts.count * Counts.stride);
            public void Dispose() { Visible?.Dispose(); Arguments?.Dispose(); Counts?.Dispose(); Visible = Arguments = Counts = null; Ready = false; }
        }
        private sealed class CameraState : IDisposable
        {
            internal GraphicsBuffer History;
            internal readonly List<Invocation> Invocations = new List<Invocation>(1);
            internal int Depth;
            internal long Bytes { get { long bytes = History == null ? 0 : (long)History.count * History.stride;
                foreach (var view in Invocations) bytes += view.Bytes; return bytes; } }
            public void Dispose() { foreach (var view in Invocations) view.Dispose(); Invocations.Clear(); History?.Dispose(); History = null; }
        }
        internal sealed class Snapshot
        {
            internal string Error;
            internal int VisibleInstances, NonemptyPackets;
            internal readonly int[] LodInstances = new int[3];
            internal long SubmittedTriangles;
            internal int[] PacketCounts;
            internal uint[] VisibleRecordIndices;
            internal long ClassificationRevision;
        }
        private static readonly int RecordsId = Shader.PropertyToID("_Records"), GroupsId = Shader.PropertyToID("_Groups"),
            CommandsId = Shader.PropertyToID("_Commands"), CountsId = Shader.PropertyToID("_Counts"), HistoryId = Shader.PropertyToID("_History"),
            VisibleId = Shader.PropertyToID("_VisibleIndices"), ArgumentsId = Shader.PropertyToID("_Arguments"),
            PlanesId = Shader.PropertyToID("_Planes"), ViewId = Shader.PropertyToID("_View"), CameraId = Shader.PropertyToID("_Camera"),
            ProjectionId = Shader.PropertyToID("_ProjectionScale"), RecordCountId = Shader.PropertyToID("_RecordCount"),
            GroupCountId = Shader.PropertyToID("_GroupCount"), CommandCountId = Shader.PropertyToID("_CommandCount"),
            InstancesId = Shader.PropertyToID("_WgVegetationInstances"), VisibleIndicesId = Shader.PropertyToID("_WgVegetationVisibleIndices"),
            UseIndicesId = Shader.PropertyToID("_WgVegetationUseVisibleIndices"), VisibleOffsetId = Shader.PropertyToID("_WgVegetationVisibleOffset");
        private readonly List<Group> _groups = new List<Group>();
        private readonly List<Packet> _packets = new List<Packet>();
        private readonly Dictionary<UnityCamera, CameraState> _cameras = new Dictionary<UnityCamera, CameraState>();
        private readonly List<UnityCamera> _dead = new List<UnityCamera>(2);
        private readonly Dictionary<Material, Material> _materials = new Dictionary<Material, Material>();
        private readonly Plane[] _planes = new Plane[6];
        private readonly Vector4[] _planeData = new Vector4[6];
        private readonly CommandBuffer _command = new CommandBuffer { name = "Vegetation instance classification" };
        private static readonly CustomSampler ClassificationSampler = CustomSampler.Create("CharacterTest.Vegetation.GpuClassification", true);
        private readonly Recorder _recorder = ClassificationSampler.GetRecorder();
        private ComputeShader _compute;
        private GraphicsBuffer _instances, _records, _groupData, _commandData;
        private GraphicsBuffer.IndirectDrawIndexedArgs[] _initialArguments;
        private uint[] _initialHistory;
        private int _clear, _classify, _finalize, _visibleCapacity;
        private bool _disposed, _recordsUploaded;
        internal int RecordCount { get; private set; }
        internal int PacketCount => _packets.Count;
        internal int GroupCount => _groups.Count;
        internal long Classifications { get; private set; }
        internal long ClassifiedRecords { get; private set; }
        internal int LastUploadedInstances { get; private set; }
        internal bool LastDispatched { get; private set; }
        internal long ResidentBytes { get { long bytes = BufferBytes(_instances) + BufferBytes(_records) + BufferBytes(_groupData) + BufferBytes(_commandData);
            foreach (var camera in _cameras.Values) bytes += camera.Bytes; return bytes; } }
        internal float GpuMilliseconds => _recorder.gpuElapsedNanoseconds / 1000000f;
        internal int GpuTimingSamples => _recorder.gpuSampleBlockCount;
        /// <summary>Explicit benchmark-only GPU timestamp recording; normal rendering does not enable it.</summary>
        internal void SetTimingRecording(bool enabled) => _recorder.enabled = enabled;
        private static long BufferBytes(GraphicsBuffer buffer) => buffer == null ? 0 : (long)buffer.count * buffer.stride;

        /// <remarks>AllocByDesign on layout creation/invalidation. No per-frame record upload or discovery.</remarks>
        internal static bool TryCreate(IReadOnlyList<CharacterTestVegetationRenderer.Batch> batches, int capacity,
            out CharacterTestVegetationGpuClassification result, out string issue)
        {
            result = null; issue = string.Empty;
            if (Marshal.SizeOf<GraphicsBuffer.IndirectDrawIndexedArgs>() != 20 || GraphicsBuffer.IndirectDrawIndexedArgs.size != 20)
            { issue = "Unsupported indexed indirect ABI; CPU classification retained."; return false; }
            var asset = UnityEngine.Resources.Load<ComputeShader>(ResourcePath);
            if (asset == null) { issue = "Vegetation classification Compute resource unavailable; CPU classification retained."; return false; }
            var candidate = new CharacterTestVegetationGpuClassification();
            candidate._recorder.enabled = false;
            try
            {
                candidate._compute = UnityEngine.Object.Instantiate(asset);
                candidate._compute.hideFlags = HideFlags.HideAndDontSave;
                candidate.Build(batches, capacity); result = candidate; return true;
            }
            catch (Exception error)
            {
                candidate.Dispose(); issue = "Vegetation Compute initialization failed; CPU classification retained: " + error.Message; return false;
            }
        }
        private void Build(IReadOnlyList<CharacterTestVegetationRenderer.Batch> batches, int capacity)
        {
            _clear = _compute.FindKernel("ClearCounts"); _classify = _compute.FindKernel("Classify"); _finalize = _compute.FindKernel("FinalizeArguments");
            foreach (var batch in batches) RecordCount += batch.Matrices.Length;
            if (RecordCount == 0) throw new InvalidOperationException("Empty vegetation layout.");
            var matrices = new CharacterTestVegetationRenderer.GpuInstance[RecordCount];
            var records = new Record[RecordCount]; _initialHistory = new uint[RecordCount];
            var keys = new Dictionary<(Mesh, Material, int), int>();
            int record = 0;
            foreach (var batch in batches)
            {
                bool lod = batch.LodMeshes[1] != null && batch.LodMeshes[2] != null;
                int[] groupIds = new int[3];
                for (int tier = 0; tier < (lod ? 3 : 1); tier++)
                {
                    var key = (batch.LodMeshes[tier], batch.Material, tier);
                    if (!keys.TryGetValue(key, out int group))
                    { group = _groups.Count; keys.Add(key, group); _groups.Add(new Group { Mesh = key.Item1, Material = key.Item2, Tier = tier, Bounds = batch.Bounds }); }
                    else _groups[group].Bounds.Encapsulate(batch.Bounds);
                    _groups[group].Population += batch.Matrices.Length; groupIds[tier] = group;
                }
                if (!lod) groupIds[1] = groupIds[2] = groupIds[0];
                for (int i = 0; i < batch.Matrices.Length; i++, record++)
                {
                    Vector3 center = batch.Centers[i], extent = batch.InstanceExtents[i];
                    matrices[record] = new CharacterTestVegetationRenderer.GpuInstance { ObjectToWorld = batch.Matrices[i], WorldToObject = batch.InverseMatrices[i] };
                    records[record] = new Record { CenterRadius = new Vector4(center.x, center.y, center.z, batch.Radii[i]),
                        ExtentsDistance = new Vector4(extent.x, extent.y, extent.z, batch.Type.DrawDistance),
                        Policy = new Vector4(batch.Type.Lod0ScreenHeight, batch.Type.Lod1ScreenHeight, batch.Type.LodHysteresis, 0),
                        Group0 = (uint)groupIds[0], Group1 = (uint)groupIds[1], Group2 = (uint)groupIds[2], HasLod = lod ? 1u : 0u };
                    _initialHistory[record] = 255;
                }
            }
            var descriptors = new UInt4[_groups.Count];
            for (int g = 0; g < _groups.Count; g++)
            {
                Group group = _groups[g]; group.VisibleOffset = _visibleCapacity;
                _visibleCapacity = checked(_visibleCapacity + group.Population);
                descriptors[g] = new UInt4((uint)group.VisibleOffset, (uint)group.Population, (uint)group.Tier);
                for (int offset = 0; offset < group.Population; offset += capacity) _packets.Add(new Packet(g, offset, capacity));
            }
            var commands = new UInt4[_packets.Count]; _initialArguments = new GraphicsBuffer.IndirectDrawIndexedArgs[_packets.Count];
            for (int p = 0; p < _packets.Count; p++)
            {
                Packet packet = _packets[p]; Mesh mesh = _groups[packet.Group].Mesh;
                commands[p] = new UInt4((uint)packet.Group, (uint)packet.Offset, (uint)packet.Capacity);
                _initialArguments[p] = new GraphicsBuffer.IndirectDrawIndexedArgs { indexCountPerInstance = mesh.GetIndexCount(0),
                    instanceCount = 0, startIndex = mesh.GetIndexStart(0), baseVertexIndex = (uint)mesh.GetBaseVertex(0), startInstance = 0 };
            }
            _instances = new GraphicsBuffer(GraphicsBuffer.Target.Structured, RecordCount, 128); _instances.SetData(matrices);
            _records = new GraphicsBuffer(GraphicsBuffer.Target.Structured, RecordCount, 64); _records.SetData(records);
            _groupData = new GraphicsBuffer(GraphicsBuffer.Target.Structured, descriptors.Length, 16); _groupData.SetData(descriptors);
            _commandData = new GraphicsBuffer(GraphicsBuffer.Target.Structured, commands.Length, 16); _commandData.SetData(commands);
            _recordsUploaded = true;
        }

        private CameraState GetCamera(UnityCamera camera)
        {
            _dead.Clear(); foreach (var pair in _cameras) if (pair.Key == null) _dead.Add(pair.Key);
            foreach (var dead in _dead) { _cameras[dead].Dispose(); _cameras.Remove(dead); }
            if (!_cameras.TryGetValue(camera, out var state))
            {
                state = new CameraState(); _cameras.Add(camera, state);
                state.History = new GraphicsBuffer(GraphicsBuffer.Target.Structured, RecordCount, 4); state.History.SetData(_initialHistory);
            }
            return state;
        }
        private Invocation CreateInvocation()
        {
            var view = new Invocation();
            try
            {
                view.Visible = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _visibleCapacity, 4);
                // IndirectArguments supplies a typed R32_UINT UAV on D3D11; Raw is an invalid flag combination.
                view.Arguments = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, _packets.Count * 5, 4);
                view.Arguments.SetData(_initialArguments);
                view.Counts = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _groups.Count, 4);
                view.Properties = new MaterialPropertyBlock[_packets.Count];
                for (int p = 0; p < _packets.Count; p++)
                {
                    Packet packet = _packets[p]; var properties = new MaterialPropertyBlock();
                    properties.SetBuffer(InstancesId, _instances); properties.SetBuffer(VisibleIndicesId, view.Visible);
                    properties.SetInteger(UseIndicesId, 1); properties.SetInteger(VisibleOffsetId, _groups[packet.Group].VisibleOffset + packet.Offset);
                    view.Properties[p] = properties;
                }
                return view;
            }
            catch { view.Dispose(); throw; }
        }
        /// <summary>O(groups + reserved packets) CPU submission, no CPU instance scan and no GPU readback.</summary>
        /// <remarks>NoAlloc after camera/invocation/material initialization.</remarks>
        internal void Submit(UnityCamera camera, int layer)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(CharacterTestVegetationGpuClassification));
            var state = GetCamera(camera); int index = state.Depth++;
            if (index == state.Invocations.Count) state.Invocations.Add(CreateInvocation());
            var view = state.Invocations[index];
            Matrix4x4 viewMatrix = camera.worldToCameraMatrix, projection = camera.projectionMatrix;
            LastUploadedInstances = _recordsUploaded ? RecordCount : 0; _recordsUploaded = false; LastDispatched = false;
            Vector3 position = camera.transform.position;
            if (!view.Ready || !view.View.Equals(viewMatrix) || !view.Projection.Equals(projection) || !view.Position.Equals(position) || view.Orthographic != camera.orthographic)
            {
                Dispatch(camera, state, view); view.View = viewMatrix; view.Projection = projection; view.Orthographic = camera.orthographic;
                view.Position = position;
                view.Ready = true; view.Revision++; LastDispatched = true; Classifications++; ClassifiedRecords += RecordCount;
            }
            foreach (var group in _groups)
            {
                if (!_materials.TryGetValue(group.Material, out Material material))
                {
                    material = new Material(group.Material) { name = group.Material.name + " (Vegetation GPU)", hideFlags = HideFlags.HideAndDontSave };
                    _materials.Add(group.Material, material);
                }
                material.CopyPropertiesFromMaterial(group.Material); material.EnableKeyword(CharacterTestVegetationRenderer.IndirectKeyword); material.enableInstancing = true;
            }
            for (int p = 0; p < _packets.Count; p++)
            {
                Group group = _groups[_packets[p].Group]; if (!group.Material.enableInstancing) continue;
                var parameters = new RenderParams(_materials[group.Material]) { camera = camera, layer = layer, worldBounds = group.Bounds,
                    shadowCastingMode = ShadowCastingMode.Off, receiveShadows = true, motionVectorMode = MotionVectorGenerationMode.ForceNoMotion,
                    lightProbeUsage = LightProbeUsage.Off, reflectionProbeUsage = ReflectionProbeUsage.Off, matProps = view.Properties[p] };
                Graphics.RenderMeshIndirect(parameters, group.Mesh, view.Arguments, 1, p);
            }
        }
        private void Dispatch(UnityCamera camera, CameraState state, Invocation view)
        {
            GeometryUtility.CalculateFrustumPlanes(camera, _planes);
            for (int i = 0; i < 6; i++) _planeData[i] = new Vector4(_planes[i].normal.x, _planes[i].normal.y, _planes[i].normal.z, _planes[i].distance);
            var cmd = _command; cmd.Clear();
            cmd.BeginSample(ClassificationSampler);
            {
                cmd.SetComputeIntParam(_compute, RecordCountId, RecordCount); cmd.SetComputeIntParam(_compute, GroupCountId, _groups.Count);
                cmd.SetComputeIntParam(_compute, CommandCountId, _packets.Count); cmd.SetComputeVectorArrayParam(_compute, PlanesId, _planeData);
                cmd.SetComputeMatrixParam(_compute, ViewId, camera.worldToCameraMatrix);
                Vector3 position = camera.transform.position;
                cmd.SetComputeVectorParam(_compute, CameraId, new Vector4(position.x, position.y, position.z, camera.orthographic ? 1 : 0));
                cmd.SetComputeFloatParam(_compute, ProjectionId, Mathf.Abs(camera.projectionMatrix.m11));
                cmd.SetComputeBufferParam(_compute, _clear, CountsId, view.Counts);
                cmd.DispatchCompute(_compute, _clear, (_groups.Count + 63) / 64, 1, 1);
                cmd.SetComputeBufferParam(_compute, _classify, RecordsId, _records); cmd.SetComputeBufferParam(_compute, _classify, GroupsId, _groupData);
                cmd.SetComputeBufferParam(_compute, _classify, CountsId, view.Counts); cmd.SetComputeBufferParam(_compute, _classify, HistoryId, state.History);
                cmd.SetComputeBufferParam(_compute, _classify, VisibleId, view.Visible);
                cmd.DispatchCompute(_compute, _classify, (RecordCount + 63) / 64, 1, 1);
                cmd.SetComputeBufferParam(_compute, _finalize, CommandsId, _commandData); cmd.SetComputeBufferParam(_compute, _finalize, CountsId, view.Counts);
                cmd.SetComputeBufferParam(_compute, _finalize, ArgumentsId, view.Arguments);
                cmd.DispatchCompute(_compute, _finalize, (_packets.Count + 63) / 64, 1, 1);
            }
            cmd.EndSample(ClassificationSampler);
            Graphics.ExecuteCommandBuffer(cmd);
        }
        internal void End(UnityCamera camera)
        { if (camera != null && _cameras.TryGetValue(camera, out var state)) state.Depth = Math.Max(0, state.Depth - 1); }

        /// <summary>Explicit asynchronous diagnostic snapshot; never called from Submit or used to decide rendering.</summary>
        /// <remarks>AllocByDesign. Snapshot describes the queued classification revision, not an assumed current frame.</remarks>
        internal bool RequestSnapshot(UnityCamera camera, Action<Snapshot> complete, bool includeRecordIndices = false)
        {
            if (complete == null) throw new ArgumentNullException(nameof(complete));
            if (_disposed || !SystemInfo.supportsAsyncGPUReadback || !_cameras.TryGetValue(camera, out var state) || state.Invocations.Count == 0) return false;
            Invocation view = state.Invocations[0]; long revision = view.Revision;
            var snapshot = new Snapshot { ClassificationRevision = revision };
            uint[] rawIndices = null;
            int pending = includeRecordIndices ? 2 : 1;
            Action finish = () =>
            {
                if (--pending != 0) return;
                if (_disposed) snapshot.Error = "Classification owner released during asynchronous diagnostic.";
                if (snapshot.Error == null && rawIndices != null)
                {
                    snapshot.VisibleRecordIndices = new uint[snapshot.VisibleInstances]; int destination = 0;
                    for (int p = 0; p < _packets.Count; p++)
                    {
                        Packet packet = _packets[p]; int source = _groups[packet.Group].VisibleOffset + packet.Offset;
                        Array.Copy(rawIndices, source, snapshot.VisibleRecordIndices, destination, snapshot.PacketCounts[p]);
                        destination += snapshot.PacketCounts[p];
                    }
                }
                complete(snapshot);
            };
            if (includeRecordIndices)
                AsyncGPUReadback.Request(view.Visible, request =>
                {
                    if (_disposed || request.hasError) snapshot.Error = "GPU visible record readback unavailable or classification owner released.";
                    else rawIndices = request.GetData<uint>().ToArray();
                    finish();
                });
            AsyncGPUReadback.Request(view.Arguments, request =>
            {
                if (_disposed || request.hasError) { snapshot.Error = "GPU readback unavailable or classification owner released."; finish(); return; }
                var data = request.GetData<uint>(); snapshot.PacketCounts = new int[_packets.Count];
                for (int p = 0; p < _packets.Count; p++)
                {
                    int count = checked((int)data[p * 5 + 1]); Packet packet = _packets[p]; Group group = _groups[packet.Group];
                    if (count > packet.Capacity) { snapshot.Error = "GPU argument exceeded configured draw capacity."; break; }
                    snapshot.PacketCounts[p] = count; snapshot.VisibleInstances += count; snapshot.LodInstances[group.Tier] += count;
                    snapshot.SubmittedTriangles += (long)group.Mesh.GetIndexCount(0) / 3 * count; if (count > 0) snapshot.NonemptyPackets++;
                }
                finish();
            });
            return true;
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            foreach (var state in _cameras.Values) state.Dispose(); _cameras.Clear(); _dead.Clear();
            _instances?.Dispose(); _records?.Dispose(); _groupData?.Dispose(); _commandData?.Dispose();
            _instances = _records = _groupData = _commandData = null;
            foreach (var material in _materials.Values) Destroy(material); _materials.Clear();
            Destroy(_compute); _compute = null; _command.Dispose(); _recorder.enabled = false;
        }
        private static void Destroy(UnityEngine.Object asset)
        { if (asset == null) return; if (Application.isPlaying) UnityEngine.Object.Destroy(asset); else UnityEngine.Object.DestroyImmediate(asset); }
    }
}

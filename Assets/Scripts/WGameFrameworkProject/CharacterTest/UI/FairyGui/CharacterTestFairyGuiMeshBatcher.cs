using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Combines adjacent FairyGUI draws without changing the authored display or hit-test tree.</summary>
    [DisallowMultipleComponent]
    internal sealed class CharacterTestFairyGuiMeshBatcher : MonoBehaviour
    {
        private static readonly ProfilerMarker RefreshMarker = new("CharacterTest.UI.MeshBatching");
        private static readonly DrawComparer Comparer = new();
        private static readonly int StencilOpId = Shader.PropertyToID("_StencilOp");
        private readonly List<MeshRenderer> _renderers = new(128);
        private readonly List<Draw> _draws = new(128);
        private readonly List<MeshRenderer> _suppressed = new(128);
        private readonly Dictionary<MeshRenderer, MeshFilter> _filters = new(128);
        private readonly List<Batch> _batches = new(32);
        private readonly HashSet<MeshRenderer> _outputs = new();
        private global::FairyGUI.Stage _stage;
        private Shader _imageShader, _textShader;

        private void OnEnable()
        {
            _imageShader = Shader.Find("FairyGUI/Image");
            _textShader = Shader.Find("FairyGUI/Text");
            _stage = global::FairyGUI.Stage.inst;
            // FairyGUI can render painting/filter snapshots during its update. Give it the originals.
            _stage.beforeUpdate += ReleaseBatches;
            _stage.afterUpdate += Refresh;
        }

        private void OnDisable()
        {
            if (_stage != null)
            {
                _stage.beforeUpdate -= ReleaseBatches;
                _stage.afterUpdate -= Refresh;
            }
            _stage = null;
            ReleaseBatches();
        }

        private void ReleaseBatches()
        {
            RestoreSources();
            foreach (var batch in _batches) if (batch.Renderer != null) batch.Renderer.enabled = false;
        }

        private void OnDestroy()
        {
            foreach (var batch in _batches)
            {
                if (batch.Mesh != null) Destroy(batch.Mesh);
                if (batch.Renderer != null) Destroy(batch.Renderer.gameObject);
            }
            _batches.Clear(); _outputs.Clear(); _filters.Clear();
        }

        private void RestoreSources()
        {
            foreach (var source in _suppressed) if (source != null) source.forceRenderingOff = false;
            _suppressed.Clear();
        }

        /// <remarks>No managed allocations after the visible hierarchy and batch sizes have warmed up.</remarks>
        internal void Refresh()
        {
            using var marker = RefreshMarker.Auto();
            RestoreSources();
            _draws.Clear();
            GetComponentsInChildren(false, _renderers);
            // Drop references to destroyed/pooled-away controls without a per-frame removal allocation.
            if (_filters.Count > Math.Max(128, _renderers.Count * 2)) _filters.Clear();
            foreach (var renderer in _renderers)
            {
                if (_outputs.Contains(renderer) || !renderer.enabled || renderer.forceRenderingOff) continue;
                if (!_filters.TryGetValue(renderer, out var filter))
                {
                    filter = renderer.GetComponent<MeshFilter>();
                    _filters.Add(renderer, filter);
                }
                var mesh = filter != null ? filter.sharedMesh : null;
                if (mesh == null || mesh.vertexCount == 0) continue;
                _draws.Add(new Draw(renderer, mesh, _imageShader, _textShader));
            }
            _draws.Sort(Comparer);
            int used = 0;
            for (int start = 0; start < _draws.Count;)
            {
                int end = start + 1;
                int vertices = _draws[start].Mesh.vertexCount;
                if (_draws[start].Eligible)
                {
                    while (end < _draws.Count && Compatible(_draws[end - 1], _draws[end])
                        && vertices + _draws[end].Mesh.vertexCount <= 60000)
                    {
                        vertices += _draws[end].Mesh.vertexCount;
                        end++;
                    }
                }
                if (end - start > 1)
                {
                    if (used == _batches.Count)
                    {
                        var batch = new Batch(transform);
                        _batches.Add(batch); _outputs.Add(batch.Renderer);
                    }
                    Build(_batches[used++], start, end);
                }
                start = end;
            }
            for (int i = used; i < _batches.Count; i++) _batches[i].Renderer.enabled = false;
        }

        private static bool Compatible(in Draw previous, in Draw next) => next.Eligible
            && previous.Material == next.Material && previous.Layer == next.Layer
            && previous.SortingLayer == next.SortingLayer && next.Order == previous.Order + 1;

        private void Build(Batch batch, int start, int end)
        {
            var first = _draws[start];
            int count = end - start;
            var combines = batch.GetBuffer(count);
            var toLocal = transform.worldToLocalMatrix;
            for (int i = 0; i < count; i++)
            {
                var draw = _draws[start + i];
                combines[i] = new CombineInstance
                {
                    mesh = draw.Mesh,
                    transform = toLocal * draw.Renderer.localToWorldMatrix
                };
            }
            batch.Mesh.Clear();
            batch.Mesh.CombineMeshes(combines, true, true, false);
            // Do not retain source meshes in reusable native-input buffers after combining.
            Array.Clear(combines, 0, count);
            batch.Renderer.gameObject.layer = first.Layer;
            batch.Renderer.sortingLayerID = first.SortingLayer;
            batch.Renderer.sortingOrder = first.Order;
            batch.Renderer.sharedMaterial = first.Material;
            batch.Renderer.enabled = true;
            for (int i = start; i < end; i++)
            {
                var renderer = _draws[i].Renderer;
                renderer.forceRenderingOff = true;
                _suppressed.Add(renderer);
            }
        }

        private readonly struct Draw
        {
            internal readonly MeshRenderer Renderer;
            internal readonly Mesh Mesh;
            internal readonly Material Material;
            internal readonly int Layer, SortingLayer, SortingLayerValue, Order;
            internal readonly bool Eligible;

            internal Draw(MeshRenderer renderer, Mesh mesh, Shader imageShader, Shader textShader)
            {
                Renderer = renderer; Mesh = mesh; Material = renderer.sharedMaterial;
                Layer = renderer.gameObject.layer; SortingLayer = renderer.sortingLayerID;
                SortingLayerValue = UnityEngine.SortingLayer.GetLayerValueFromID(SortingLayer);
                Order = renderer.sortingOrder;
                Shader shader = Material != null ? Material.shader : null;
                // Custom card shaders, stencil writers/erasers and per-renderer properties stay separate.
                // Shared clipped materials keep the same stencil/clip state and world-space vertices.
                Eligible = shader != null && (shader == imageShader || shader == textShader)
                    && mesh.isReadable && mesh.subMeshCount == 1
                    && mesh.GetTopology(0) == MeshTopology.Triangles && !renderer.HasPropertyBlock()
                    && Material.GetFloat(StencilOpId) == (float)StencilOp.Keep;
            }
        }

        private sealed class DrawComparer : IComparer<Draw>
        {
            public int Compare(Draw a, Draw b)
            {
                int layer = a.SortingLayerValue.CompareTo(b.SortingLayerValue);
                return layer != 0 ? layer : a.Order.CompareTo(b.Order);
            }
        }

        private sealed class Batch
        {
            internal readonly Mesh Mesh;
            internal readonly MeshRenderer Renderer;
            private readonly Dictionary<int, CombineInstance[]> _buffers = new();

            internal Batch(Transform parent)
            {
                var go = new GameObject("FairyGUI Combined Draw") { hideFlags = HideFlags.DontSave };
                go.transform.SetParent(parent, false);
                Mesh = new Mesh { name = "FairyGUI Combined Draw", hideFlags = HideFlags.DontSave };
                Mesh.MarkDynamic();
                go.AddComponent<MeshFilter>().sharedMesh = Mesh;
                Renderer = go.AddComponent<MeshRenderer>();
                Renderer.enabled = false;
                Renderer.shadowCastingMode = ShadowCastingMode.Off;
                Renderer.receiveShadows = false;
                Renderer.lightProbeUsage = LightProbeUsage.Off;
                Renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }

            internal CombineInstance[] GetBuffer(int count)
            {
                if (!_buffers.TryGetValue(count, out var buffer))
                {
                    buffer = new CombineInstance[count];
                    _buffers.Add(count, buffer);
                }
                return buffer;
            }
        }
    }
}

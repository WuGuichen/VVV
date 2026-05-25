using System;
using System.Collections.Generic;
using System.Text;
using MxFramework.Diagnostics;
using UnityEngine;

namespace MxFramework.Rendering
{
    public enum MxMaterialChannel
    {
        HitFlash = 0,
        StatusTint = 1,
        DissolveProgress = 2,
        OutlineState = 3,
        WetnessOverride = 4,
        BridgeCustom = 5,
        DebugOverlay = 6
    }

    public enum MaterialBindingScopeKind
    {
        Renderer = 0,
        RendererSubMesh = 1,
        SubjectHierarchy = 2
    }

    public interface IMaterialBindingHub
    {
        MaterialBinding Bind(MxRenderSubjectId subject, MxMaterialChannel channel, in MaterialBindingScope scope);
        bool Release(MaterialBinding binding);
        void Release(MxRenderSubjectId subject);
        MaterialBindingDiagnosticsSnapshot CaptureDiagnostics();
    }

    public interface IMaterialBindingWriter
    {
        void SetFloat(MaterialBinding binding, int propertyId, float value);
        void SetColor(MaterialBinding binding, int propertyId, Color value);
        void SetVector(MaterialBinding binding, int propertyId, Vector4 value);
        void SetTexture(MaterialBinding binding, int propertyId, Texture texture);
        void Pulse(MaterialBinding binding, int propertyId, in MaterialBindingCurveDescriptor curve, float duration);
    }

    public readonly struct MaterialBinding : IEquatable<MaterialBinding>
    {
        internal MaterialBinding(int id, MxRenderSubjectId subject, MxMaterialChannel channel)
        {
            Id = id;
            Subject = subject;
            Channel = channel;
        }

        public int Id { get; }
        public MxRenderSubjectId Subject { get; }
        public MxMaterialChannel Channel { get; }
        public bool IsValid => Id > 0 && Subject.IsValid;

        public bool Equals(MaterialBinding other)
        {
            return Id == other.Id && Subject == other.Subject && Channel == other.Channel;
        }

        public override bool Equals(object obj)
        {
            return obj is MaterialBinding other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = Id;
                hashCode = (hashCode * 397) ^ Subject.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)Channel;
                return hashCode;
            }
        }

        public static bool operator ==(MaterialBinding left, MaterialBinding right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(MaterialBinding left, MaterialBinding right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct MaterialBindingScope
    {
        private readonly Renderer[] _renderers;

        private MaterialBindingScope(MaterialBindingScopeKind kind, Renderer renderer, int materialIndex, Renderer[] renderers)
        {
            Kind = kind;
            Renderer = renderer;
            MaterialIndex = materialIndex;
            _renderers = renderers;
        }

        public MaterialBindingScopeKind Kind { get; }
        public Renderer Renderer { get; }
        public int MaterialIndex { get; }
        public IReadOnlyList<Renderer> Renderers => _renderers ?? Array.Empty<Renderer>();

        public static MaterialBindingScope ForRenderer(Renderer renderer)
        {
            return new MaterialBindingScope(MaterialBindingScopeKind.Renderer, renderer, -1, null);
        }

        public static MaterialBindingScope ForRendererSubMesh(Renderer renderer, int materialIndex)
        {
            return new MaterialBindingScope(MaterialBindingScopeKind.RendererSubMesh, renderer, materialIndex, null);
        }

        public static MaterialBindingScope ForSubjectHierarchy(IEnumerable<Renderer> renderers)
        {
            if (renderers == null)
                return new MaterialBindingScope(MaterialBindingScopeKind.SubjectHierarchy, null, -1, Array.Empty<Renderer>());

            var values = new List<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                if (renderer != null)
                    values.Add(renderer);
            }

            return new MaterialBindingScope(MaterialBindingScopeKind.SubjectHierarchy, null, -1, values.ToArray());
        }

        internal IReadOnlyList<MaterialBindingTarget> ResolveTargets()
        {
            var targets = new List<MaterialBindingTarget>();
            switch (Kind)
            {
                case MaterialBindingScopeKind.Renderer:
                    AddRendererTargets(targets, Renderer);
                    break;
                case MaterialBindingScopeKind.RendererSubMesh:
                    if (Renderer != null && MaterialIndex >= 0 && MaterialIndex < Renderer.sharedMaterials.Length)
                        targets.Add(new MaterialBindingTarget(Renderer, MaterialIndex));
                    break;
                case MaterialBindingScopeKind.SubjectHierarchy:
                    IReadOnlyList<Renderer> renderers = Renderers;
                    for (int i = 0; i < renderers.Count; i++)
                        AddRendererTargets(targets, renderers[i]);
                    break;
            }

            return targets;
        }

        private static void AddRendererTargets(List<MaterialBindingTarget> targets, Renderer renderer)
        {
            if (renderer == null)
                return;

            int materialCount = renderer.sharedMaterials.Length;
            if (materialCount <= 0)
            {
                targets.Add(new MaterialBindingTarget(renderer, -1));
                return;
            }

            for (int i = 0; i < materialCount; i++)
                targets.Add(new MaterialBindingTarget(renderer, i));
        }
    }

    public readonly struct MaterialBindingCurvePoint
    {
        public MaterialBindingCurvePoint(float time, float value)
        {
            Time = time;
            Value = value;
        }

        public float Time { get; }
        public float Value { get; }
    }

    public readonly struct MaterialBindingCurveDescriptor
    {
        private readonly MaterialBindingCurvePoint[] _points;

        public MaterialBindingCurveDescriptor(IReadOnlyList<MaterialBindingCurvePoint> points, bool loop = false)
        {
            Loop = loop;
            if (points == null || points.Count == 0)
            {
                _points = Array.Empty<MaterialBindingCurvePoint>();
                return;
            }

            _points = new MaterialBindingCurvePoint[points.Count];
            for (int i = 0; i < points.Count; i++)
                _points[i] = points[i];
        }

        public IReadOnlyList<MaterialBindingCurvePoint> Points => _points ?? Array.Empty<MaterialBindingCurvePoint>();
        public bool Loop { get; }

        public static MaterialBindingCurveDescriptor Linear(float startValue, float endValue)
        {
            return new MaterialBindingCurveDescriptor(
                new[]
                {
                    new MaterialBindingCurvePoint(0f, startValue),
                    new MaterialBindingCurvePoint(1f, endValue)
                });
        }

        internal float Evaluate(float normalizedTime)
        {
            IReadOnlyList<MaterialBindingCurvePoint> points = Points;
            if (points.Count == 0)
                return 0f;

            float t = Loop ? Mathf.Repeat(normalizedTime, 1f) : Mathf.Clamp01(normalizedTime);
            if (t <= points[0].Time)
                return points[0].Value;

            for (int i = 1; i < points.Count; i++)
            {
                MaterialBindingCurvePoint previous = points[i - 1];
                MaterialBindingCurvePoint next = points[i];
                if (t > next.Time)
                    continue;

                float range = next.Time - previous.Time;
                if (range <= 0f)
                    return next.Value;

                return Mathf.Lerp(previous.Value, next.Value, (t - previous.Time) / range);
            }

            return points[points.Count - 1].Value;
        }
    }

    public sealed class MaterialBindingHub : IMaterialBindingHub, IMaterialBindingWriter
    {
        private const int ChannelCount = 7;

        private readonly MxRenderSubjectRegistry _subjects;
        private readonly Dictionary<int, BindingState> _bindings = new Dictionary<int, BindingState>();
        private readonly Dictionary<SubjectChannelKey, int> _bindingBySubjectChannel = new Dictionary<SubjectChannelKey, int>();
        private readonly Dictionary<MaterialBindingTarget, List<BindingState>> _targetBindings = new Dictionary<MaterialBindingTarget, List<BindingState>>();
        private readonly Stack<MaterialPropertyBlock> _propertyBlockPool = new Stack<MaterialPropertyBlock>();
        private readonly List<MaterialBindingDuplicateWarning> _duplicateWarnings = new List<MaterialBindingDuplicateWarning>();
        private readonly List<MaterialBindingTarget> _dirtyTargets = new List<MaterialBindingTarget>();
        private readonly HashSet<MaterialBindingTarget> _dirtyTargetSet = new HashSet<MaterialBindingTarget>();
        private readonly int[] _channelCounts = new int[ChannelCount];

        private int _nextBindingId = 1;
        private int _lastAppliedTargetCount;
        private int _lastMergedPropertyCount;
        private int _totalPoolGets;
        private int _poolHits;

        public MaterialBindingHub(MxRenderSubjectRegistry subjects = null)
        {
            _subjects = subjects;
            if (_subjects != null)
                _subjects.SubjectReleased += Release;
        }

        public MaterialBinding Bind(MxRenderSubjectId subject, MxMaterialChannel channel, in MaterialBindingScope scope)
        {
            if (!subject.IsValid || !IsKnownChannel(channel))
                return default;

            if (_subjects != null && !_subjects.TryResolve(subject, out var _))
                return default;

            IReadOnlyList<MaterialBindingTarget> targets = scope.ResolveTargets();
            if (targets.Count == 0)
                return default;

            var key = new SubjectChannelKey(subject, channel);
            if (_bindingBySubjectChannel.TryGetValue(key, out int existingId) && _bindings.TryGetValue(existingId, out BindingState existing))
            {
                _duplicateWarnings.Add(new MaterialBindingDuplicateWarning(subject, channel, existing.Binding.Id));
                Release(existing.Binding);
            }

            var binding = new MaterialBinding(_nextBindingId++, subject, channel);
            var state = new BindingState(binding, targets);
            _bindings[binding.Id] = state;
            _bindingBySubjectChannel[key] = binding.Id;
            _channelCounts[(int)channel]++;

            for (int i = 0; i < targets.Count; i++)
            {
                MaterialBindingTarget target = targets[i];
                if (!_targetBindings.TryGetValue(target, out List<BindingState> bindings))
                {
                    bindings = new List<BindingState>();
                    _targetBindings[target] = bindings;
                }

                bindings.Add(state);
                MarkDirty(target);
            }

            return binding;
        }

        public bool Release(MaterialBinding binding)
        {
            if (!binding.IsValid || !_bindings.TryGetValue(binding.Id, out BindingState state))
                return false;

            _bindings.Remove(binding.Id);
            _bindingBySubjectChannel.Remove(new SubjectChannelKey(binding.Subject, binding.Channel));
            if (IsKnownChannel(binding.Channel))
                _channelCounts[(int)binding.Channel]--;

            for (int i = 0; i < state.Targets.Count; i++)
            {
                MaterialBindingTarget target = state.Targets[i];
                if (_targetBindings.TryGetValue(target, out List<BindingState> bindings))
                {
                    bindings.Remove(state);
                    if (bindings.Count == 0)
                        _targetBindings.Remove(target);
                }

                MarkDirty(target);
            }

            return true;
        }

        public void Release(MxRenderSubjectId subject)
        {
            if (!subject.IsValid)
                return;

            var toRelease = new List<MaterialBinding>();
            foreach (BindingState state in _bindings.Values)
            {
                if (state.Binding.Subject == subject)
                    toRelease.Add(state.Binding);
            }

            for (int i = 0; i < toRelease.Count; i++)
                Release(toRelease[i]);
        }

        public void SetFloat(MaterialBinding binding, int propertyId, float value)
        {
            if (!TryGetWritable(binding, propertyId, out BindingState state))
                return;

            state.Properties[propertyId] = MaterialPropertyValue.Float(value);
            MarkDirty(state);
        }

        public void SetColor(MaterialBinding binding, int propertyId, Color value)
        {
            if (!TryGetWritable(binding, propertyId, out BindingState state))
                return;

            state.Properties[propertyId] = MaterialPropertyValue.Color(value);
            MarkDirty(state);
        }

        public void SetVector(MaterialBinding binding, int propertyId, Vector4 value)
        {
            if (!TryGetWritable(binding, propertyId, out BindingState state))
                return;

            state.Properties[propertyId] = MaterialPropertyValue.Vector(value);
            MarkDirty(state);
        }

        public void SetTexture(MaterialBinding binding, int propertyId, Texture texture)
        {
            if (!TryGetWritable(binding, propertyId, out BindingState state))
                return;

            state.Properties[propertyId] = MaterialPropertyValue.Texture(texture);
            MarkDirty(state);
        }

        public void Pulse(MaterialBinding binding, int propertyId, in MaterialBindingCurveDescriptor curve, float duration)
        {
            if (!TryGetWritable(binding, propertyId, out BindingState state))
                return;

            float value = duration > 0f ? curve.Evaluate(1f) : curve.Evaluate(0f);
            state.Properties[propertyId] = MaterialPropertyValue.Float(value);
            MarkDirty(state);
        }

        public void Flush()
        {
            _lastAppliedTargetCount = 0;
            _lastMergedPropertyCount = 0;

            for (int i = 0; i < _dirtyTargets.Count; i++)
            {
                MaterialBindingTarget target = _dirtyTargets[i];
                if (target.Renderer == null)
                    continue;

                MaterialPropertyBlock block = GetPropertyBlock();
                block.Clear();

                if (_targetBindings.TryGetValue(target, out List<BindingState> bindings))
                {
                    bindings.Sort(BindingStateComparer.Instance);
                    for (int bindingIndex = 0; bindingIndex < bindings.Count; bindingIndex++)
                    {
                        foreach (KeyValuePair<int, MaterialPropertyValue> property in bindings[bindingIndex].Properties)
                        {
                            property.Value.Apply(block, property.Key);
                            _lastMergedPropertyCount++;
                        }
                    }
                }

                if (target.MaterialIndex >= 0)
                    target.Renderer.SetPropertyBlock(block, target.MaterialIndex);
                else
                    target.Renderer.SetPropertyBlock(block);
                _lastAppliedTargetCount++;
                ReturnPropertyBlock(block);
            }

            _dirtyTargets.Clear();
            _dirtyTargetSet.Clear();
        }

        public MaterialBindingDiagnosticsSnapshot CaptureDiagnostics()
        {
            return new MaterialBindingDiagnosticsSnapshot(
                _bindings.Count,
                _targetBindings.Count,
                _lastAppliedTargetCount,
                _lastMergedPropertyCount,
                _propertyBlockPool.Count,
                _totalPoolGets,
                _poolHits,
                CreateChannelCounts(),
                _duplicateWarnings);
        }

        private bool TryGetWritable(MaterialBinding binding, int propertyId, out BindingState state)
        {
            state = null;
            if (propertyId == 0 || !binding.IsValid || !_bindings.TryGetValue(binding.Id, out state))
                return false;

            return state.Binding == binding;
        }

        private void MarkDirty(BindingState state)
        {
            for (int i = 0; i < state.Targets.Count; i++)
                MarkDirty(state.Targets[i]);
        }

        private void MarkDirty(MaterialBindingTarget target)
        {
            if (_dirtyTargetSet.Add(target))
                _dirtyTargets.Add(target);
        }

        private MaterialPropertyBlock GetPropertyBlock()
        {
            _totalPoolGets++;
            if (_propertyBlockPool.Count > 0)
            {
                _poolHits++;
                return _propertyBlockPool.Pop();
            }

            return new MaterialPropertyBlock();
        }

        private void ReturnPropertyBlock(MaterialPropertyBlock block)
        {
            block.Clear();
            _propertyBlockPool.Push(block);
        }

        private IReadOnlyList<MaterialBindingChannelCount> CreateChannelCounts()
        {
            var values = new MaterialBindingChannelCount[ChannelCount];
            for (int i = 0; i < ChannelCount; i++)
                values[i] = new MaterialBindingChannelCount((MxMaterialChannel)i, _channelCounts[i]);
            return values;
        }

        private static bool IsKnownChannel(MxMaterialChannel channel)
        {
            int value = (int)channel;
            return value >= 0 && value < ChannelCount;
        }

        private sealed class BindingState
        {
            public BindingState(MaterialBinding binding, IReadOnlyList<MaterialBindingTarget> targets)
            {
                Binding = binding;
                Targets = new List<MaterialBindingTarget>(targets);
            }

            public MaterialBinding Binding { get; }
            public List<MaterialBindingTarget> Targets { get; }
            public Dictionary<int, MaterialPropertyValue> Properties { get; } = new Dictionary<int, MaterialPropertyValue>();
        }

        private sealed class BindingStateComparer : IComparer<BindingState>
        {
            public static readonly BindingStateComparer Instance = new BindingStateComparer();

            public int Compare(BindingState x, BindingState y)
            {
                int xId = x != null ? x.Binding.Id : 0;
                int yId = y != null ? y.Binding.Id : 0;
                return xId.CompareTo(yId);
            }
        }
    }

    public readonly struct MaterialBindingDiagnosticsSnapshot
    {
        private readonly List<MaterialBindingChannelCount> _channelCounts;
        private readonly List<MaterialBindingDuplicateWarning> _duplicateWarnings;

        public MaterialBindingDiagnosticsSnapshot(
            int bindingCount,
            int targetCount,
            int lastAppliedTargetCount,
            int lastMergedPropertyCount,
            int pooledPropertyBlockCount,
            int totalPropertyBlockGets,
            int propertyBlockPoolHits,
            IReadOnlyList<MaterialBindingChannelCount> channelCounts,
            IReadOnlyList<MaterialBindingDuplicateWarning> duplicateWarnings)
        {
            BindingCount = bindingCount;
            TargetCount = targetCount;
            LastAppliedTargetCount = lastAppliedTargetCount;
            LastMergedPropertyCount = lastMergedPropertyCount;
            PooledPropertyBlockCount = pooledPropertyBlockCount;
            TotalPropertyBlockGets = totalPropertyBlockGets;
            PropertyBlockPoolHits = propertyBlockPoolHits;
            _channelCounts = channelCounts != null ? new List<MaterialBindingChannelCount>(channelCounts) : new List<MaterialBindingChannelCount>();
            _duplicateWarnings = duplicateWarnings != null ? new List<MaterialBindingDuplicateWarning>(duplicateWarnings) : new List<MaterialBindingDuplicateWarning>();
        }

        public int BindingCount { get; }
        public int TargetCount { get; }
        public int LastAppliedTargetCount { get; }
        public int LastMergedPropertyCount { get; }
        public int PooledPropertyBlockCount { get; }
        public int TotalPropertyBlockGets { get; }
        public int PropertyBlockPoolHits { get; }
        public float PropertyBlockPoolHitRate => TotalPropertyBlockGets > 0 ? (float)PropertyBlockPoolHits / TotalPropertyBlockGets : 0f;
        public IReadOnlyList<MaterialBindingChannelCount> ChannelCounts => _channelCounts;
        public IReadOnlyList<MaterialBindingDuplicateWarning> DuplicateWarnings => _duplicateWarnings;

        public int CountFor(MxMaterialChannel channel)
        {
            for (int i = 0; i < _channelCounts.Count; i++)
            {
                if (_channelCounts[i].Channel == channel)
                    return _channelCounts[i].Count;
            }

            return 0;
        }
    }

    public readonly struct MaterialBindingChannelCount
    {
        public MaterialBindingChannelCount(MxMaterialChannel channel, int count)
        {
            Channel = channel;
            Count = count;
        }

        public MxMaterialChannel Channel { get; }
        public int Count { get; }
    }

    public readonly struct MaterialBindingDuplicateWarning
    {
        public MaterialBindingDuplicateWarning(MxRenderSubjectId subject, MxMaterialChannel channel, int replacedBindingId)
        {
            Subject = subject;
            Channel = channel;
            ReplacedBindingId = replacedBindingId;
        }

        public MxRenderSubjectId Subject { get; }
        public MxMaterialChannel Channel { get; }
        public int ReplacedBindingId { get; }
    }

    public sealed class MaterialBindingHubDebugSource : IRenderingDebugSource
    {
        private readonly IMaterialBindingHub _hub;

        public MaterialBindingHubDebugSource(IMaterialBindingHub hub, string name = "Rendering")
        {
            _hub = hub ?? throw new ArgumentNullException(nameof(hub));
            Name = string.IsNullOrWhiteSpace(name) ? "Rendering" : name;
        }

        public string Name { get; }
        public FrameworkDebugMode Mode => FrameworkDebugMode.Runtime;
        public bool IsAvailable => true;

        public FrameworkDebugSnapshot CreateSnapshot()
        {
            return new FrameworkDebugSnapshot(
                Name,
                Mode,
                new[] { new FrameworkDebugSection(RenderingDebugSectionNames.MaterialBindings, Format(_hub.CaptureDiagnostics())) });
        }

        private static string Format(MaterialBindingDiagnosticsSnapshot snapshot)
        {
            var builder = new StringBuilder();
            builder.Append("bindings: ").Append(snapshot.BindingCount).Append('\n');
            builder.Append("targets: ").Append(snapshot.TargetCount).Append('\n');
            builder.Append("lastAppliedTargets: ").Append(snapshot.LastAppliedTargetCount).Append('\n');
            builder.Append("lastMergedProperties: ").Append(snapshot.LastMergedPropertyCount).Append('\n');
            builder.Append("pooledPropertyBlocks: ").Append(snapshot.PooledPropertyBlockCount).Append('\n');
            builder.Append("propertyBlockPoolHitRate: ").Append(snapshot.PropertyBlockPoolHitRate).Append('\n');
            builder.Append("duplicateWarnings: ").Append(snapshot.DuplicateWarnings.Count);

            for (int i = 0; i < snapshot.ChannelCounts.Count; i++)
            {
                MaterialBindingChannelCount count = snapshot.ChannelCounts[i];
                builder.Append('\n').Append(count.Channel).Append(": ").Append(count.Count);
            }

            for (int i = 0; i < snapshot.DuplicateWarnings.Count; i++)
            {
                MaterialBindingDuplicateWarning warning = snapshot.DuplicateWarnings[i];
                builder.Append('\n')
                    .Append("duplicate ")
                    .Append(warning.Subject)
                    .Append('/')
                    .Append(warning.Channel)
                    .Append(" replaced=")
                    .Append(warning.ReplacedBindingId);
            }

            return builder.ToString();
        }
    }

    internal readonly struct MaterialBindingTarget : IEquatable<MaterialBindingTarget>
    {
        public MaterialBindingTarget(Renderer renderer, int materialIndex)
        {
            Renderer = renderer;
            MaterialIndex = materialIndex;
        }

        public Renderer Renderer { get; }
        public int MaterialIndex { get; }

        public bool Equals(MaterialBindingTarget other)
        {
            return ReferenceEquals(Renderer, other.Renderer) && MaterialIndex == other.MaterialIndex;
        }

        public override bool Equals(object obj)
        {
            return obj is MaterialBindingTarget other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Renderer != null ? Renderer.GetHashCode() : 0) * 397) ^ MaterialIndex;
            }
        }
    }

    internal readonly struct SubjectChannelKey : IEquatable<SubjectChannelKey>
    {
        public SubjectChannelKey(MxRenderSubjectId subject, MxMaterialChannel channel)
        {
            Subject = subject;
            Channel = channel;
        }

        public MxRenderSubjectId Subject { get; }
        public MxMaterialChannel Channel { get; }

        public bool Equals(SubjectChannelKey other)
        {
            return Subject == other.Subject && Channel == other.Channel;
        }

        public override bool Equals(object obj)
        {
            return obj is SubjectChannelKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Subject.GetHashCode() * 397) ^ (int)Channel;
            }
        }
    }

    internal readonly struct MaterialPropertyValue
    {
        private readonly MaterialPropertyValueKind _kind;
        private readonly float _floatValue;
        private readonly Color _colorValue;
        private readonly Vector4 _vectorValue;
        private readonly Texture _textureValue;

        private MaterialPropertyValue(MaterialPropertyValueKind kind, float floatValue, Color colorValue, Vector4 vectorValue, Texture textureValue)
        {
            _kind = kind;
            _floatValue = floatValue;
            _colorValue = colorValue;
            _vectorValue = vectorValue;
            _textureValue = textureValue;
        }

        public static MaterialPropertyValue Float(float value)
        {
            return new MaterialPropertyValue(MaterialPropertyValueKind.Float, value, default, default, null);
        }

        public static MaterialPropertyValue Color(Color value)
        {
            return new MaterialPropertyValue(MaterialPropertyValueKind.Color, 0f, value, default, null);
        }

        public static MaterialPropertyValue Vector(Vector4 value)
        {
            return new MaterialPropertyValue(MaterialPropertyValueKind.Vector, 0f, default, value, null);
        }

        public static MaterialPropertyValue Texture(Texture value)
        {
            return new MaterialPropertyValue(MaterialPropertyValueKind.Texture, 0f, default, default, value);
        }

        public void Apply(MaterialPropertyBlock block, int propertyId)
        {
            switch (_kind)
            {
                case MaterialPropertyValueKind.Float:
                    block.SetFloat(propertyId, _floatValue);
                    break;
                case MaterialPropertyValueKind.Color:
                    block.SetColor(propertyId, _colorValue);
                    break;
                case MaterialPropertyValueKind.Vector:
                    block.SetVector(propertyId, _vectorValue);
                    break;
                case MaterialPropertyValueKind.Texture:
                    block.SetTexture(propertyId, _textureValue);
                    break;
            }
        }
    }

    internal enum MaterialPropertyValueKind
    {
        Float = 0,
        Color = 1,
        Vector = 2,
        Texture = 3
    }
}

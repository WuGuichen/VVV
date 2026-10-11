using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using MxFramework.Resources;
using MxFramework.Runtime;

namespace MxFramework.Vfx
{
    internal readonly struct VfxPreparedResourceFingerprint
    {
        private VfxPreparedResourceFingerprint(RuntimeHashDigest contentDigest)
        {
            ContentDigest = contentDigest;
        }

        public RuntimeHashDigest ContentDigest { get; }

        public static VfxPreparedResourceFingerprint Prepare(ResourceKey resource)
        {
            // ResourceKey is an address, not a numeric identity. Preserve the exact canonical
            // UTF-16 content once at the definition/import boundary and never parse or format it
            // from the steady-state hash path.
            return new VfxPreparedResourceFingerprint(
                RuntimeHashDigest.ComputeContentDigest(resource.ToString()));
        }
    }

    public static class VfxSourceDomains
    {
        public const int Manual = 1;
        public const int Gameplay = 10;
        public const int CombatAction = 20;
        public const int CombatHit = 21;
        public const int ProjectilePresentation = 22;
        public const int ActionPresentation = 30;
    }

    public readonly struct VfxDefinitionId : IComparable<VfxDefinitionId>, IEquatable<VfxDefinitionId>
    {
        public VfxDefinitionId(int value)
        {
            Value = value;
        }

        public int Value { get; }
        public bool IsValid => Value > 0;

        public int CompareTo(VfxDefinitionId other)
        {
            return Value.CompareTo(other.Value);
        }

        public bool Equals(VfxDefinitionId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is VfxDefinitionId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return IsValid ? Value.ToString() : "Invalid";
        }

        public static bool operator ==(VfxDefinitionId left, VfxDefinitionId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(VfxDefinitionId left, VfxDefinitionId right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct VfxInstanceId : IComparable<VfxInstanceId>, IEquatable<VfxInstanceId>
    {
        public VfxInstanceId(long value)
        {
            Value = value;
        }

        public long Value { get; }
        public bool IsValid => Value > 0L;

        public int CompareTo(VfxInstanceId other)
        {
            return Value.CompareTo(other.Value);
        }

        public bool Equals(VfxInstanceId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is VfxInstanceId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return IsValid ? Value.ToString() : "Invalid";
        }

        public static bool operator ==(VfxInstanceId left, VfxInstanceId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(VfxInstanceId left, VfxInstanceId right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct VfxVector3 : IEquatable<VfxVector3>
    {
        public const long Scale = 1000000L;

        public static readonly VfxVector3 Zero = new VfxVector3(0L, 0L, 0L);
        public static readonly VfxVector3 Forward = new VfxVector3(0L, 0L, Scale);
        public static readonly VfxVector3 One = new VfxVector3(Scale, Scale, Scale);

        private VfxVector3(long rawX, long rawY, long rawZ)
        {
            RawX = rawX;
            RawY = rawY;
            RawZ = rawZ;
        }

        public long RawX { get; }
        public long RawY { get; }
        public long RawZ { get; }

        public static VfxVector3 FromRaw(long rawX, long rawY, long rawZ)
        {
            return new VfxVector3(rawX, rawY, rawZ);
        }

        public static VfxVector3 FromInt(int x, int y, int z)
        {
            return new VfxVector3(checked(x * Scale), checked(y * Scale), checked(z * Scale));
        }

        public bool Equals(VfxVector3 other)
        {
            return RawX == other.RawX && RawY == other.RawY && RawZ == other.RawZ;
        }

        public override bool Equals(object obj)
        {
            return obj is VfxVector3 other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = RawX.GetHashCode();
                hash = (hash * 397) ^ RawY.GetHashCode();
                hash = (hash * 397) ^ RawZ.GetHashCode();
                return hash;
            }
        }

        internal void Contribute(RuntimeHashAccumulator accumulator)
        {
            accumulator.AddLong(VfxVector3HashFields.X, RawX);
            accumulator.AddLong(VfxVector3HashFields.Y, RawY);
            accumulator.AddLong(VfxVector3HashFields.Z, RawZ);
        }

        private static class VfxVector3HashFields
        {
            public const int X = 1;
            public const int Y = 2;
            public const int Z = 3;
        }
    }

    /// <summary>Frozen schema ids for the VFX numeric hash protocol.</summary>
    public static class VfxHashSchemas
    {
        public const int Pose = 1;
        public const int Anchor = 2;
        public const int ParameterBlock = 3;
        public const int SpawnRequest = 4;
        public const int StopRequest = 5;
        public const int RebuildRequest = 6;
        public const int Instance = 7;
    }

    public readonly struct VfxPose : IEquatable<VfxPose>
    {
        public static readonly VfxPose Identity = new VfxPose(VfxVector3.Zero, VfxVector3.Forward);

        public VfxPose(VfxVector3 position, VfxVector3 forward)
            : this(position, forward, VfxVector3.Zero, VfxVector3.One)
        {
        }

        public VfxPose(
            VfxVector3 position,
            VfxVector3 forward,
            VfxVector3 rotationEulerDegrees,
            VfxVector3 scale)
        {
            Position = position;
            Forward = forward.Equals(VfxVector3.Zero) ? VfxVector3.Forward : forward;
            RotationEulerDegrees = rotationEulerDegrees;
            Scale = scale.Equals(VfxVector3.Zero) ? VfxVector3.One : scale;
        }

        public VfxVector3 Position { get; }
        public VfxVector3 Forward { get; }
        public VfxVector3 RotationEulerDegrees { get; }
        public VfxVector3 Scale { get; }

        public bool Equals(VfxPose other)
        {
            return Position.Equals(other.Position)
                && Forward.Equals(other.Forward)
                && RotationEulerDegrees.Equals(other.RotationEulerDegrees)
                && Scale.Equals(other.Scale);
        }

        public override bool Equals(object obj)
        {
            return obj is VfxPose other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (Position.GetHashCode() * 397) ^ Forward.GetHashCode();
                hash = (hash * 397) ^ RotationEulerDegrees.GetHashCode();
                hash = (hash * 397) ^ Scale.GetHashCode();
                return hash;
            }
        }

        internal void Contribute(RuntimeHashAccumulator accumulator)
        {
            accumulator.BeginObject(VfxPoseHashFields.Pose, VfxHashSchemas.Pose, 1);
            Position.Contribute(accumulator);
            Forward.Contribute(accumulator);
            RotationEulerDegrees.Contribute(accumulator);
            Scale.Contribute(accumulator);
            accumulator.EndObject();
        }

        private static class VfxPoseHashFields
        {
            public const int Pose = 1;
        }
    }

    public enum VfxAnchorKind
    {
        World = 0,
        SourceEntity = 1,
        TargetEntity = 2,
        Attached = 3
    }

    public readonly struct VfxAnchor : IEquatable<VfxAnchor>
    {
        public VfxAnchor(VfxAnchorKind kind, int sourceId, int targetId, int socketId, VfxPose pose)
        {
            Kind = kind;
            SourceId = sourceId < 0 ? 0 : sourceId;
            TargetId = targetId < 0 ? 0 : targetId;
            SocketId = socketId < 0 ? 0 : socketId;
            Pose = pose;
        }

        public VfxAnchorKind Kind { get; }
        public int SourceId { get; }
        public int TargetId { get; }
        public int SocketId { get; }
        public VfxPose Pose { get; }

        public static VfxAnchor World(VfxPose pose)
        {
            return new VfxAnchor(VfxAnchorKind.World, 0, 0, 0, pose);
        }

        public static VfxAnchor Source(int sourceId, VfxPose pose)
        {
            return new VfxAnchor(VfxAnchorKind.SourceEntity, sourceId, 0, 0, pose);
        }

        public static VfxAnchor Target(int sourceId, int targetId, VfxPose pose)
        {
            return new VfxAnchor(VfxAnchorKind.TargetEntity, sourceId, targetId, 0, pose);
        }

        public bool Equals(VfxAnchor other)
        {
            return Kind == other.Kind
                && SourceId == other.SourceId
                && TargetId == other.TargetId
                && SocketId == other.SocketId
                && Pose.Equals(other.Pose);
        }

        public override bool Equals(object obj)
        {
            return obj is VfxAnchor other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Kind;
                hash = (hash * 397) ^ SourceId;
                hash = (hash * 397) ^ TargetId;
                hash = (hash * 397) ^ SocketId;
                hash = (hash * 397) ^ Pose.GetHashCode();
                return hash;
            }
        }

        internal void Contribute(RuntimeHashAccumulator accumulator)
        {
            accumulator.BeginObject(VfxAnchorHashFields.Anchor, VfxHashSchemas.Anchor, 1);
            accumulator.AddInt(VfxAnchorHashFields.Kind, (int)Kind);
            accumulator.AddInt(VfxAnchorHashFields.SourceId, SourceId);
            accumulator.AddInt(VfxAnchorHashFields.TargetId, TargetId);
            accumulator.AddInt(VfxAnchorHashFields.SocketId, SocketId);
            Pose.Contribute(accumulator);
            accumulator.EndObject();
        }

        private static class VfxAnchorHashFields
        {
            public const int Anchor = 1;
            public const int Kind = 2;
            public const int SourceId = 3;
            public const int TargetId = 4;
            public const int SocketId = 5;
        }
    }

    public readonly struct VfxParameterBlock : IEquatable<VfxParameterBlock>
    {
        public static readonly VfxParameterBlock Empty = new VfxParameterBlock();

        public VfxParameterBlock(int value0, int value1 = 0, int value2 = 0, int value3 = 0)
        {
            Value0 = value0;
            Value1 = value1;
            Value2 = value2;
            Value3 = value3;
        }

        public int Value0 { get; }
        public int Value1 { get; }
        public int Value2 { get; }
        public int Value3 { get; }

        public bool Equals(VfxParameterBlock other)
        {
            return Value0 == other.Value0
                && Value1 == other.Value1
                && Value2 == other.Value2
                && Value3 == other.Value3;
        }

        public override bool Equals(object obj)
        {
            return obj is VfxParameterBlock other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Value0;
                hash = (hash * 397) ^ Value1;
                hash = (hash * 397) ^ Value2;
                hash = (hash * 397) ^ Value3;
                return hash;
            }
        }

        internal void Contribute(RuntimeHashAccumulator accumulator)
        {
            accumulator.BeginObject(VfxParameterBlockHashFields.ParameterBlock, VfxHashSchemas.ParameterBlock, 1);
            accumulator.AddInt(VfxParameterBlockHashFields.Value0, Value0);
            accumulator.AddInt(VfxParameterBlockHashFields.Value1, Value1);
            accumulator.AddInt(VfxParameterBlockHashFields.Value2, Value2);
            accumulator.AddInt(VfxParameterBlockHashFields.Value3, Value3);
            accumulator.EndObject();
        }

        private static class VfxParameterBlockHashFields
        {
            public const int ParameterBlock = 1;
            public const int Value0 = 2;
            public const int Value1 = 3;
            public const int Value2 = 4;
            public const int Value3 = 5;
        }
    }

    public sealed class VfxDefinition
    {
        public VfxDefinition(
            VfxDefinitionId definitionId,
            string stableName,
            ResourceKey prefabResource,
            int defaultLifetimeFrames,
            string definitionHash = "")
        {
            if (!definitionId.IsValid)
            {
                throw new ArgumentOutOfRangeException(nameof(definitionId), "VFX definition id must be positive.");
            }

            if (defaultLifetimeFrames < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(defaultLifetimeFrames), "VFX default lifetime cannot be negative.");
            }

            DefinitionId = definitionId;
            StableName = stableName ?? string.Empty;
            PrefabResource = prefabResource;
            DefaultLifetimeFrames = defaultLifetimeFrames;
            DefinitionHash = string.IsNullOrWhiteSpace(definitionHash)
                ? DefinitionId.Value + ":" + StableName + ":" + PrefabResource
                : definitionHash;
            DefinitionContentDigest = RuntimePreparedDigest.PrepareExisting(DefinitionHash);
            PrefabResourceFingerprint = VfxPreparedResourceFingerprint.Prepare(PrefabResource);
        }

        public VfxDefinitionId DefinitionId { get; }
        public string StableName { get; }
        public ResourceKey PrefabResource { get; }
        public int DefaultLifetimeFrames { get; }
        public string DefinitionHash { get; }
        internal RuntimePreparedDigest DefinitionContentDigest { get; }
        internal VfxPreparedResourceFingerprint PrefabResourceFingerprint { get; }

        internal void Contribute(RuntimeContentHashAccumulator accumulator, string prefix)
        {
            accumulator.AddInt(prefix + ".id", DefinitionId.Value);
            accumulator.AddStringStable(prefix + ".stableName", StableName);
            accumulator.AddStringStable(prefix + ".prefab", PrefabResource.ToString());
            accumulator.AddInt(prefix + ".lifetime", DefaultLifetimeFrames);
            accumulator.AddStringStable(prefix + ".hash", DefinitionHash);
        }
    }

    public sealed class VfxDefinitionRegistry
    {
        private readonly Dictionary<int, VfxDefinition> _definitions = new Dictionary<int, VfxDefinition>();
        private bool _hasStableHash;
        private long _stableHash;

        public int Count => _definitions.Count;

        public bool TryRegister(VfxDefinition definition, out string failure)
        {
            if (definition == null)
            {
                failure = "VFX definition is missing.";
                return false;
            }

            int key = definition.DefinitionId.Value;
            if (_definitions.ContainsKey(key))
            {
                failure = "Duplicate VFX definition id: " + key + ".";
                return false;
            }

            _definitions.Add(key, definition);
            _hasStableHash = false;
            failure = string.Empty;
            return true;
        }

        public bool TryGet(VfxDefinitionId definitionId, out VfxDefinition definition)
        {
            return _definitions.TryGetValue(definitionId.Value, out definition);
        }

        public IReadOnlyList<VfxDefinition> CreateSnapshot()
        {
            var values = new List<VfxDefinition>(_definitions.Values);
            values.Sort((left, right) => left.DefinitionId.CompareTo(right.DefinitionId));
            return new ReadOnlyCollection<VfxDefinition>(values);
        }

        public long ComputeStableHash()
        {
            if (_hasStableHash)
                return _stableHash;

            var accumulator = new RuntimeContentHashAccumulator();
            IReadOnlyList<VfxDefinition> definitions = CreateSnapshot();
            accumulator.AddInt("vfx.definition.count", definitions.Count);
            for (int i = 0; i < definitions.Count; i++)
                definitions[i].Contribute(accumulator, "vfx.definition." + i);

            _stableHash = accumulator.ToHash();
            _hasStableHash = true;
            return _stableHash;
        }
    }

    public readonly struct VfxTriggerContext
    {
        public VfxTriggerContext(
            RuntimeFrame frame,
            int sourceDomain,
            int triggerId,
            int sourceId,
            int targetId,
            long sourceFrame,
            int localFrame,
            int sourceOrder,
            VfxAnchor anchor,
            VfxParameterBlock parameters,
            RuntimeTraceIdentity traceId)
        {
            Frame = frame;
            SourceDomain = sourceDomain;
            TriggerId = triggerId < 0 ? 0 : triggerId;
            SourceId = sourceId < 0 ? 0 : sourceId;
            TargetId = targetId < 0 ? 0 : targetId;
            SourceFrame = sourceFrame < 0L ? 0L : sourceFrame;
            LocalFrame = localFrame < 0 ? 0 : localFrame;
            SourceOrder = sourceOrder < 0 ? 0 : sourceOrder;
            Anchor = anchor;
            Parameters = parameters;
            TraceId = traceId;
        }

        public RuntimeFrame Frame { get; }
        public int SourceDomain { get; }
        public int TriggerId { get; }
        public int SourceId { get; }
        public int TargetId { get; }
        public long SourceFrame { get; }
        public int LocalFrame { get; }
        public int SourceOrder { get; }
        public VfxAnchor Anchor { get; }
        public VfxParameterBlock Parameters { get; }
        public RuntimeTraceIdentity TraceId { get; }
    }

    public interface IVfxTriggerResolver
    {
        int Resolve(in VfxTriggerContext context, List<VfxSpawnRequest> output);
    }

    public readonly struct VfxTriggerMapping : IEquatable<VfxTriggerMapping>
    {
        public VfxTriggerMapping(int sourceDomain, int triggerId, VfxDefinitionId definitionId, int lifetimeFramesOverride = 0)
        {
            SourceDomain = sourceDomain;
            TriggerId = triggerId < 0 ? 0 : triggerId;
            DefinitionId = definitionId;
            LifetimeFramesOverride = lifetimeFramesOverride < 0 ? 0 : lifetimeFramesOverride;
        }

        public int SourceDomain { get; }
        public int TriggerId { get; }
        public VfxDefinitionId DefinitionId { get; }
        public int LifetimeFramesOverride { get; }

        public bool Equals(VfxTriggerMapping other)
        {
            return SourceDomain == other.SourceDomain
                && TriggerId == other.TriggerId
                && DefinitionId.Equals(other.DefinitionId)
                && LifetimeFramesOverride == other.LifetimeFramesOverride;
        }

        public override bool Equals(object obj)
        {
            return obj is VfxTriggerMapping other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = SourceDomain;
                hash = (hash * 397) ^ TriggerId;
                hash = (hash * 397) ^ DefinitionId.Value;
                hash = (hash * 397) ^ LifetimeFramesOverride;
                return hash;
            }
        }
    }

    public sealed class VfxTriggerMapResolver : IVfxTriggerResolver
    {
        private readonly Dictionary<TriggerKey, VfxTriggerMapping> _mappings = new Dictionary<TriggerKey, VfxTriggerMapping>();

        public VfxTriggerMapResolver Add(VfxTriggerMapping mapping)
        {
            _mappings[new TriggerKey(mapping.SourceDomain, mapping.TriggerId)] = mapping;
            return this;
        }

        public int Resolve(in VfxTriggerContext context, List<VfxSpawnRequest> output)
        {
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            if (!_mappings.TryGetValue(new TriggerKey(context.SourceDomain, context.TriggerId), out VfxTriggerMapping mapping))
                return 0;

            output.Add(new VfxSpawnRequest(
                context.Frame,
                context.SourceDomain,
                context.SourceFrame,
                context.LocalFrame,
                context.SourceOrder,
                context.TriggerId,
                context.SourceId,
                context.TargetId,
                mapping.DefinitionId,
                context.Anchor,
                context.Parameters,
                mapping.LifetimeFramesOverride,
                context.TraceId));
            return 1;
        }

        private readonly struct TriggerKey : IEquatable<TriggerKey>
        {
            public TriggerKey(int sourceDomain, int triggerId)
            {
                SourceDomain = sourceDomain;
                TriggerId = triggerId;
            }

            private int SourceDomain { get; }
            private int TriggerId { get; }

            public bool Equals(TriggerKey other)
            {
                return SourceDomain == other.SourceDomain && TriggerId == other.TriggerId;
            }

            public override bool Equals(object obj)
            {
                return obj is TriggerKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (SourceDomain * 397) ^ TriggerId;
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using MxFramework.Config;
using MxFramework.Resources;

namespace MxFramework.Items
{
    public readonly struct ItemDefinitionId : IEquatable<ItemDefinitionId>, IFormattable
    {
        public ItemDefinitionId(int value)
        {
            Value = value;
        }

        public int Value { get; }
        public bool IsValid => Value > 0;
        public bool Equals(ItemDefinitionId other) { return Value == other.Value; }
        public override bool Equals(object obj) { return obj is ItemDefinitionId other && Equals(other); }
        public override int GetHashCode() { return Value; }
        public override string ToString() { return Value.ToString(); }
        public string ToString(string format, IFormatProvider formatProvider) { return Value.ToString(format, formatProvider); }
    }

    public enum ItemKind
    {
        Custom = 0,
        Consumable = 1,
        Equipment = 2,
        Material = 3,
        Currency = 4,
        Quest = 5,
        Container = 6
    }

    public readonly struct ItemTagSet
    {
        private readonly int[] _tags;

        public ItemTagSet(int[] tags)
        {
            _tags = Normalize(tags);
        }

        public IReadOnlyList<int> Tags => _tags ?? Array.Empty<int>();
        public int Count => _tags == null ? 0 : _tags.Length;

        public bool Contains(int tagId)
        {
            if (tagId <= 0 || _tags == null)
                return false;
            return Array.BinarySearch(_tags, tagId) >= 0;
        }

        internal void Contribute(StableHashBuilder builder, string prefix)
        {
            int count = _tags == null ? 0 : _tags.Length;
            builder.AddInt(prefix + ".count", count);
            for (int i = 0; i < count; i++)
                builder.AddInt(prefix + "." + i, _tags[i]);
        }

        private static int[] Normalize(int[] source)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<int>();

            var values = new List<int>(source.Length);
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] > 0)
                    values.Add(source[i]);
            }

            if (values.Count == 0)
                return Array.Empty<int>();

            values.Sort();
            for (int i = values.Count - 1; i > 0; i--)
            {
                if (values[i] == values[i - 1])
                    values.RemoveAt(i);
            }

            return values.ToArray();
        }
    }

    public readonly struct ItemStackPolicy
    {
        public ItemStackPolicy(int maxStackCount)
        {
            if (maxStackCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxStackCount), "Item max stack count must be positive.");
            MaxStackCount = maxStackCount;
        }

        public int MaxStackCount { get; }
        public bool CanStack => MaxStackCount > 1;

        public static ItemStackPolicy ForKind(ItemKind kind, int maxStackCount = 0)
        {
            if (maxStackCount <= 0 && kind == ItemKind.Equipment)
                maxStackCount = 1;
            return new ItemStackPolicy(maxStackCount);
        }
    }

    public readonly struct ItemResourceRef
    {
        public ItemResourceRef(ResourceKey key, string usageId = "", string preloadGroupId = "")
        {
            Key = key;
            UsageId = usageId ?? string.Empty;
            PreloadGroupId = preloadGroupId ?? string.Empty;
        }

        public ResourceKey Key { get; }
        public string UsageId { get; }
        public string PreloadGroupId { get; }
        public bool IsValid => Key.IsValid;

        internal void Contribute(StableHashBuilder builder, string prefix)
        {
            builder.AddString(prefix + ".id", Key.Id);
            builder.AddString(prefix + ".type", Key.TypeId);
            builder.AddString(prefix + ".variant", Key.Variant);
            builder.AddString(prefix + ".package", Key.PackageId);
            builder.AddString(prefix + ".usage", UsageId);
            builder.AddString(prefix + ".preload", PreloadGroupId);
        }
    }

    public readonly struct ItemProfileRef
    {
        public ItemProfileRef(string profileTypeId, string profileId)
        {
            ProfileTypeId = profileTypeId ?? string.Empty;
            ProfileId = profileId ?? string.Empty;
        }

        public string ProfileTypeId { get; }
        public string ProfileId { get; }
        public bool IsValid => !string.IsNullOrEmpty(ProfileTypeId) && !string.IsNullOrEmpty(ProfileId);

        internal void Contribute(StableHashBuilder builder, string prefix)
        {
            builder.AddString(prefix + ".type", ProfileTypeId);
            builder.AddString(prefix + ".id", ProfileId);
        }
    }

    /// <summary>通用物品定义；分配行为：定义构建、schema 和注册表加载为 LowFreqAlloc。</summary>
    public sealed class ItemDefinition : IConfigData
    {
        public const string TableName = "ItemDefinition";

        public ItemDefinition(
            ItemDefinitionId definitionId,
            string stableId,
            LocalizedTextKey nameKey,
            LocalizedTextKey descriptionKey,
            ItemKind kind,
            ItemTagSet tags,
            ItemStackPolicy stackPolicy,
            ItemResourceRef[] resourceRefs = null,
            ItemProfileRef[] profileRefs = null)
        {
            if (!definitionId.IsValid)
                throw new ArgumentOutOfRangeException(nameof(definitionId), "Item definition id must be positive.");
            if (string.IsNullOrWhiteSpace(stableId))
                throw new ArgumentException("Item stable id cannot be empty.", nameof(stableId));

            DefinitionId = definitionId;
            StableId = stableId;
            NameKey = nameKey;
            DescriptionKey = descriptionKey;
            Kind = kind;
            Tags = tags;
            if (stackPolicy.MaxStackCount <= 0)
            {
                if (kind != ItemKind.Equipment)
                    throw new ArgumentOutOfRangeException(nameof(stackPolicy), "Item stack policy must define a positive max stack count.");

                stackPolicy = ItemStackPolicy.ForKind(kind);
            }

            StackPolicy = stackPolicy;
            ResourceRefs = resourceRefs ?? Array.Empty<ItemResourceRef>();
            ProfileRefs = profileRefs ?? Array.Empty<ItemProfileRef>();
        }

        public int Id => DefinitionId.Value;
        public ItemDefinitionId DefinitionId { get; }
        public string StableId { get; }
        public LocalizedTextKey NameKey { get; }
        public LocalizedTextKey DescriptionKey { get; }
        public ItemKind Kind { get; }
        public ItemTagSet Tags { get; }
        public ItemStackPolicy StackPolicy { get; }
        public int MaxStackCount => StackPolicy.MaxStackCount;
        public ItemResourceRef[] ResourceRefs { get; }
        public ItemProfileRef[] ProfileRefs { get; }

        public static ConfigSchema<ItemDefinition> CreateSchema()
        {
            var schema = new ConfigSchema<ItemDefinition>(
                TableName,
                displayName: "Item Definition",
                description: "通用物品定义；运行时库存保存 definition id 和 count，不保存 Unity 对象。");
            schema.AddField(new ConfigField(nameof(Id), ConfigFieldType.Integer, displayName: "物品编号", required: true));
            schema.AddField(new ConfigField(nameof(StableId), ConfigFieldType.String, displayName: "稳定 ID", required: true));
            schema.AddField(new ConfigField(nameof(NameKey), ConfigFieldType.LocalizedText, displayName: "名称 key"));
            schema.AddField(new ConfigField(nameof(DescriptionKey), ConfigFieldType.LocalizedText, displayName: "描述 key"));
            schema.AddField(new ConfigField(nameof(Kind), ConfigFieldType.Enum, displayName: "物品类型", required: true, enumId: "items.ItemKind"));
            schema.AddField(new ConfigField(nameof(Tags), ConfigFieldType.Custom, displayName: "标签", valueType: typeof(ItemTagSet)));
            schema.AddField(new ConfigField(nameof(MaxStackCount), ConfigFieldType.Integer, displayName: "最大堆叠", required: true));
            schema.AddField(new ConfigField(nameof(ResourceRefs), ConfigFieldType.Custom, displayName: "资源引用", valueType: typeof(ItemResourceRef[])));
            schema.AddField(new ConfigField(nameof(ProfileRefs), ConfigFieldType.Custom, displayName: "Profile 引用", valueType: typeof(ItemProfileRef[])));
            return schema;
        }

        internal void Contribute(StableHashBuilder builder, string prefix)
        {
            builder.AddInt(prefix + ".id", DefinitionId.Value);
            builder.AddString(prefix + ".stable", StableId);
            builder.AddString(prefix + ".name", NameKey.Value);
            builder.AddString(prefix + ".desc", DescriptionKey.Value);
            builder.AddInt(prefix + ".kind", (int)Kind);
            Tags.Contribute(builder, prefix + ".tags");
            builder.AddInt(prefix + ".maxStack", MaxStackCount);
            builder.AddInt(prefix + ".resources.count", ResourceRefs.Length);
            for (int i = 0; i < ResourceRefs.Length; i++)
                ResourceRefs[i].Contribute(builder, prefix + ".resources." + i);
            builder.AddInt(prefix + ".profiles.count", ProfileRefs.Length);
            for (int i = 0; i < ProfileRefs.Length; i++)
                ProfileRefs[i].Contribute(builder, prefix + ".profiles." + i);
        }
    }

    public enum ItemDefinitionRegistryStatus
    {
        Success = 0,
        NullDefinition = 1,
        DuplicateId = 2,
        DuplicateStableId = 3
    }

    public readonly struct ItemDefinitionRegistryResult
    {
        public ItemDefinitionRegistryResult(ItemDefinitionRegistryStatus status, ItemDefinition definition)
        {
            Status = status;
            Definition = definition;
        }

        public ItemDefinitionRegistryStatus Status { get; }
        public ItemDefinition Definition { get; }
        public bool Success => Status == ItemDefinitionRegistryStatus.Success;
    }

    /// <summary>物品定义注册表；分配行为：注册和稳定 hash 计算为 LowFreqAlloc，运行时库存操作只读查询。</summary>
    public sealed class ItemDefinitionRegistry
    {
        private readonly Dictionary<int, ItemDefinition> _byId = new Dictionary<int, ItemDefinition>();
        private readonly Dictionary<string, ItemDefinition> _byStableId = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
        private readonly List<ItemDefinition> _definitions = new List<ItemDefinition>();

        public int Count => _definitions.Count;
        public IReadOnlyList<ItemDefinition> Definitions => _definitions;

        public ItemDefinitionRegistryResult Register(ItemDefinition definition)
        {
            if (definition == null)
                return new ItemDefinitionRegistryResult(ItemDefinitionRegistryStatus.NullDefinition, null);
            if (_byId.ContainsKey(definition.Id))
                return new ItemDefinitionRegistryResult(ItemDefinitionRegistryStatus.DuplicateId, definition);
            if (_byStableId.ContainsKey(definition.StableId))
                return new ItemDefinitionRegistryResult(ItemDefinitionRegistryStatus.DuplicateStableId, definition);

            _byId.Add(definition.Id, definition);
            _byStableId.Add(definition.StableId, definition);
            _definitions.Add(definition);
            _definitions.Sort(CompareDefinitions);
            return new ItemDefinitionRegistryResult(ItemDefinitionRegistryStatus.Success, definition);
        }

        public bool TryGet(ItemDefinitionId id, out ItemDefinition definition)
        {
            return _byId.TryGetValue(id.Value, out definition);
        }

        public bool TryGetByStableId(string stableId, out ItemDefinition definition)
        {
            return _byStableId.TryGetValue(stableId ?? string.Empty, out definition);
        }

        public long ComputeStableHash()
        {
            var builder = new StableHashBuilder();
            builder.AddInt("item.definitions.count", _definitions.Count);
            for (int i = 0; i < _definitions.Count; i++)
                _definitions[i].Contribute(builder, "item.definition." + i);
            return builder.ToHash();
        }

        private static int CompareDefinitions(ItemDefinition left, ItemDefinition right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return -1;
            if (right == null)
                return 1;
            return left.Id.CompareTo(right.Id);
        }
    }

    internal sealed class StableHashBuilder
    {
        private const ulong OffsetBasis = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;
        private ulong _hash = OffsetBasis;

        public void AddInt(string key, int value)
        {
            AddString(key);
            AddByte(1);
            unchecked
            {
                AddByte((byte)value);
                AddByte((byte)(value >> 8));
                AddByte((byte)(value >> 16));
                AddByte((byte)(value >> 24));
            }
        }

        public void AddString(string key, string value)
        {
            AddString(key);
            AddByte(2);
            AddString(value ?? string.Empty);
        }

        public long ToHash()
        {
            unchecked
            {
                return (long)_hash;
            }
        }

        private void AddString(string value)
        {
            value = value ?? string.Empty;
            AddIntBytes(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                AddByte((byte)c);
                AddByte((byte)(c >> 8));
            }
        }

        private void AddIntBytes(int value)
        {
            unchecked
            {
                AddByte((byte)value);
                AddByte((byte)(value >> 8));
                AddByte((byte)(value >> 16));
                AddByte((byte)(value >> 24));
            }
        }

        private void AddByte(byte value)
        {
            unchecked
            {
                _hash ^= value;
                _hash *= Prime;
            }
        }
    }
}

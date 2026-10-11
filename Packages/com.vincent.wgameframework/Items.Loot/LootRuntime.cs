using System;
using System.Collections.Generic;
using MxFramework.Runtime;

namespace MxFramework.Items
{
    public readonly struct LootTableId : IEquatable<LootTableId>, IFormattable
    {
        public LootTableId(int value)
        {
            Value = value;
        }

        public int Value { get; }
        public bool IsValid => Value > 0;
        public bool Equals(LootTableId other) { return Value == other.Value; }
        public override bool Equals(object obj) { return obj is LootTableId other && Equals(other); }
        public override int GetHashCode() { return Value; }
        public override string ToString() { return Value.ToString(); }
        public string ToString(string format, IFormatProvider formatProvider) { return Value.ToString(format, formatProvider); }
    }

    public sealed class LootTableDefinition
    {
        public LootTableDefinition(LootTableId tableId, string stableId, LootGroupDefinition[] groups)
        {
            TableId = tableId;
            StableId = stableId ?? string.Empty;
            Groups = groups ?? Array.Empty<LootGroupDefinition>();
        }

        public LootTableId TableId { get; }
        public string StableId { get; }
        public LootGroupDefinition[] Groups { get; }
    }

    public sealed class LootGroupDefinition
    {
        public LootGroupDefinition(
            string stableId,
            int rollCountMin,
            int rollCountMax,
            int activationChanceBasisPoints,
            int emptyWeight,
            LootEntryDefinition[] entries)
        {
            StableId = stableId ?? string.Empty;
            RollCountMin = rollCountMin;
            RollCountMax = rollCountMax;
            ActivationChanceBasisPoints = activationChanceBasisPoints;
            EmptyWeight = emptyWeight;
            Entries = entries ?? Array.Empty<LootEntryDefinition>();
        }

        public string StableId { get; }
        public int RollCountMin { get; }
        public int RollCountMax { get; }
        public int ActivationChanceBasisPoints { get; }
        public int EmptyWeight { get; }
        public LootEntryDefinition[] Entries { get; }
    }

    public sealed class LootEntryDefinition
    {
        public LootEntryDefinition(
            ItemDefinitionId itemDefinitionId,
            int weight,
            int countMin,
            int countMax,
            int minSourceLevel = 0,
            int maxSourceLevel = 0,
            ItemTagSet requiredContextTags = default,
            ItemTagSet blockedContextTags = default)
        {
            ItemDefinitionId = itemDefinitionId;
            Weight = weight;
            CountMin = countMin;
            CountMax = countMax;
            MinSourceLevel = minSourceLevel;
            MaxSourceLevel = maxSourceLevel;
            RequiredContextTags = requiredContextTags;
            BlockedContextTags = blockedContextTags;
        }

        public ItemDefinitionId ItemDefinitionId { get; }
        public int Weight { get; }
        public int CountMin { get; }
        public int CountMax { get; }
        public int MinSourceLevel { get; }
        public int MaxSourceLevel { get; }
        public ItemTagSet RequiredContextTags { get; }
        public ItemTagSet BlockedContextTags { get; }
    }

    public readonly struct LootContext
    {
        public const int BasisPointsScale = 10000;

        public LootContext(
            string sourceStableId,
            int sourceLevel,
            ItemTagSet contextTags,
            int dropRateMultiplierBasisPoints = BasisPointsScale,
            int quantityMultiplierBasisPoints = BasisPointsScale)
        {
            SourceStableId = sourceStableId ?? string.Empty;
            SourceLevel = sourceLevel;
            ContextTags = contextTags;
            DropRateMultiplierBasisPoints = dropRateMultiplierBasisPoints;
            QuantityMultiplierBasisPoints = quantityMultiplierBasisPoints;
        }

        public string SourceStableId { get; }
        public int SourceLevel { get; }
        public ItemTagSet ContextTags { get; }
        public int DropRateMultiplierBasisPoints { get; }
        public int QuantityMultiplierBasisPoints { get; }
    }

    public readonly struct LootItemQuantity : IEquatable<LootItemQuantity>
    {
        public LootItemQuantity(ItemDefinitionId itemDefinitionId, int count)
        {
            if (!itemDefinitionId.IsValid)
                throw new ArgumentOutOfRangeException(nameof(itemDefinitionId));
            if (count <= 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            ItemDefinitionId = itemDefinitionId;
            Count = count;
        }

        public ItemDefinitionId ItemDefinitionId { get; }
        public int Count { get; }
        public bool Equals(LootItemQuantity other) { return ItemDefinitionId.Equals(other.ItemDefinitionId) && Count == other.Count; }
        public override bool Equals(object obj) { return obj is LootItemQuantity other && Equals(other); }
        public override int GetHashCode() { return (ItemDefinitionId.GetHashCode() * 397) ^ Count; }
    }

    public enum LootResolveStatus
    {
        Success = 0,
        InvalidTable = 1,
        InvalidContext = 2,
        InvalidRandom = 3,
        InvalidGroup = 4,
        InvalidEntry = 5,
        DefinitionMissing = 6
    }

    public readonly struct LootDiagnostic
    {
        public LootDiagnostic(LootResolveStatus status, string path, string message)
        {
            Status = status;
            Path = path ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public LootResolveStatus Status { get; }
        public string Path { get; }
        public string Message { get; }
    }

    public sealed class LootResolveResult
    {
        public LootResolveResult(LootResolveStatus status, LootItemQuantity[] items, LootDiagnostic[] diagnostics)
        {
            Status = status;
            Items = items ?? Array.Empty<LootItemQuantity>();
            Diagnostics = diagnostics ?? Array.Empty<LootDiagnostic>();
        }

        public LootResolveStatus Status { get; }
        public bool Success => Status == LootResolveStatus.Success;
        public LootItemQuantity[] Items { get; }
        public LootDiagnostic[] Diagnostics { get; }
    }

    /// <summary>Deterministic loot resolver for item quantity results.</summary>
    /// <remarks>Allocation: LowFreqAlloc; resolution builds temporary diagnostics and output arrays.</remarks>
    public sealed class LootResolver
    {
        private readonly ItemDefinitionRegistry _definitions;

        public LootResolver(ItemDefinitionRegistry definitions)
        {
            _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
        }

        public LootResolveResult Resolve(LootTableDefinition table, LootContext context, IDeterministicRandom random)
        {
            var diagnostics = new List<LootDiagnostic>();
            if (!ValidateTable(table, diagnostics) || !ValidateContext(context, diagnostics) || random == null)
            {
                if (random == null)
                    diagnostics.Add(new LootDiagnostic(LootResolveStatus.InvalidRandom, "random", "Loot random source is required."));
                return Failed(diagnostics);
            }

            var results = new List<LootItemQuantity>();
            for (int groupIndex = 0; groupIndex < table.Groups.Length; groupIndex++)
            {
                LootGroupDefinition group = table.Groups[groupIndex];
                int activationChance = ApplyMultiplier(
                    group.ActivationChanceBasisPoints,
                    context.DropRateMultiplierBasisPoints,
                    LootContext.BasisPointsScale);
                if (activationChance <= 0 || random.NextInt(0, LootContext.BasisPointsScale) >= activationChance)
                    continue;

                int rollCount = group.RollCountMin == group.RollCountMax
                    ? group.RollCountMin
                    : random.NextInt(group.RollCountMin, group.RollCountMax + 1);
                for (int rollIndex = 0; rollIndex < rollCount; rollIndex++)
                    RollGroup(group, context, random, results);
            }

            results.Sort(CompareLootItems);
            return new LootResolveResult(LootResolveStatus.Success, results.ToArray(), diagnostics.ToArray());
        }

        private void RollGroup(
            LootGroupDefinition group,
            LootContext context,
            IDeterministicRandom random,
            List<LootItemQuantity> results)
        {
            int totalWeight = group.EmptyWeight;
            for (int i = 0; i < group.Entries.Length; i++)
            {
                LootEntryDefinition entry = group.Entries[i];
                if (entry.Weight > 0 && Matches(entry, context))
                    totalWeight += entry.Weight;
            }

            if (totalWeight <= 0)
                return;

            int roll = random.NextInt(0, totalWeight);
            if (roll < group.EmptyWeight)
                return;

            int cursor = group.EmptyWeight;
            for (int i = 0; i < group.Entries.Length; i++)
            {
                LootEntryDefinition entry = group.Entries[i];
                if (entry.Weight <= 0 || !Matches(entry, context))
                    continue;

                cursor += entry.Weight;
                if (roll >= cursor)
                    continue;

                int count = entry.CountMin == entry.CountMax
                    ? entry.CountMin
                    : random.NextInt(entry.CountMin, entry.CountMax + 1);
                count = ApplyQuantityMultiplier(count, context.QuantityMultiplierBasisPoints);
                if (count > 0)
                    AddOrMerge(results, entry.ItemDefinitionId, count);
                return;
            }
        }

        private bool ValidateTable(LootTableDefinition table, List<LootDiagnostic> diagnostics)
        {
            if (table == null)
            {
                diagnostics.Add(new LootDiagnostic(LootResolveStatus.InvalidTable, "table", "Loot table is required."));
                return false;
            }

            if (!table.TableId.IsValid || string.IsNullOrWhiteSpace(table.StableId) || table.Groups == null || table.Groups.Length == 0)
                diagnostics.Add(new LootDiagnostic(LootResolveStatus.InvalidTable, table.StableId, "Loot table id, stable id, and groups are required."));

            for (int groupIndex = 0; table.Groups != null && groupIndex < table.Groups.Length; groupIndex++)
            {
                LootGroupDefinition group = table.Groups[groupIndex];
                string groupPath = "groups[" + groupIndex + "]";
                if (group == null)
                {
                    diagnostics.Add(new LootDiagnostic(LootResolveStatus.InvalidGroup, groupPath, "Loot group cannot be null."));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(group.StableId)
                    || group.RollCountMin < 0
                    || group.RollCountMax < group.RollCountMin
                    || (group.RollCountMin != group.RollCountMax && group.RollCountMax == int.MaxValue)
                    || group.ActivationChanceBasisPoints < 0
                    || group.ActivationChanceBasisPoints > LootContext.BasisPointsScale
                    || group.EmptyWeight < 0
                    || group.Entries == null
                    || group.Entries.Length == 0)
                {
                    diagnostics.Add(new LootDiagnostic(LootResolveStatus.InvalidGroup, groupPath, "Loot group has invalid roll, activation, empty weight, or entries."));
                }

                long totalWeight = group.EmptyWeight;
                for (int entryIndex = 0; group.Entries != null && entryIndex < group.Entries.Length; entryIndex++)
                {
                    LootEntryDefinition entry = group.Entries[entryIndex];
                    string entryPath = groupPath + ".entries[" + entryIndex + "]";
                    if (entry == null)
                    {
                        diagnostics.Add(new LootDiagnostic(LootResolveStatus.InvalidEntry, entryPath, "Loot entry cannot be null."));
                        continue;
                    }

                    if (!entry.ItemDefinitionId.IsValid || entry.Weight < 0 || entry.CountMin <= 0 || entry.CountMax < entry.CountMin
                        || (entry.CountMin != entry.CountMax && entry.CountMax == int.MaxValue))
                    {
                        diagnostics.Add(new LootDiagnostic(LootResolveStatus.InvalidEntry, entryPath, "Loot entry item id, weight, and count range must be valid."));
                        continue;
                    }

                    totalWeight += entry.Weight;

                    if (!_definitions.TryGet(entry.ItemDefinitionId, out _))
                    {
                        diagnostics.Add(new LootDiagnostic(LootResolveStatus.DefinitionMissing, entryPath, "Loot entry item definition is missing."));
                    }
                }
                if (totalWeight > int.MaxValue)
                    diagnostics.Add(new LootDiagnostic(LootResolveStatus.InvalidGroup, groupPath,
                        "Loot group total weight exceeds the supported Int32 random range."));
            }

            return diagnostics.Count == 0;
        }

        private static bool ValidateContext(LootContext context, List<LootDiagnostic> diagnostics)
        {
            if (context.SourceLevel < 0
                || context.DropRateMultiplierBasisPoints < 0
                || context.QuantityMultiplierBasisPoints < 0)
            {
                diagnostics.Add(new LootDiagnostic(LootResolveStatus.InvalidContext, "context", "Loot context level and multipliers must be non-negative."));
                return false;
            }

            return true;
        }

        private static LootResolveResult Failed(List<LootDiagnostic> diagnostics)
        {
            LootResolveStatus status = diagnostics.Count > 0 ? diagnostics[0].Status : LootResolveStatus.InvalidTable;
            return new LootResolveResult(status, Array.Empty<LootItemQuantity>(), diagnostics.ToArray());
        }

        private static bool Matches(LootEntryDefinition entry, LootContext context)
        {
            if (entry.MinSourceLevel > 0 && context.SourceLevel < entry.MinSourceLevel)
                return false;
            if (entry.MaxSourceLevel > 0 && context.SourceLevel > entry.MaxSourceLevel)
                return false;
            if (!ContainsAll(context.ContextTags, entry.RequiredContextTags))
                return false;
            if (ContainsAny(context.ContextTags, entry.BlockedContextTags))
                return false;
            return true;
        }

        private static bool ContainsAll(ItemTagSet source, ItemTagSet required)
        {
            IReadOnlyList<int> requiredTags = required.Tags;
            for (int i = 0; i < requiredTags.Count; i++)
            {
                if (!source.Contains(requiredTags[i]))
                    return false;
            }

            return true;
        }

        private static bool ContainsAny(ItemTagSet source, ItemTagSet blocked)
        {
            IReadOnlyList<int> blockedTags = blocked.Tags;
            for (int i = 0; i < blockedTags.Count; i++)
            {
                if (source.Contains(blockedTags[i]))
                    return true;
            }

            return false;
        }

        private static int ApplyMultiplier(int value, int multiplierBasisPoints, int maximum)
        {
            long scaled = (long)value * multiplierBasisPoints / LootContext.BasisPointsScale;
            if (scaled < 0L)
                return 0;
            if (scaled > maximum)
                return maximum;
            return (int)scaled;
        }

        private static int ApplyQuantityMultiplier(int count, int multiplierBasisPoints)
        {
            long scaled = ((long)count * multiplierBasisPoints + LootContext.BasisPointsScale - 1) / LootContext.BasisPointsScale;
            if (scaled <= 0L)
                return 0;
            if (scaled > int.MaxValue)
                return int.MaxValue;
            return (int)scaled;
        }

        private static void AddOrMerge(List<LootItemQuantity> results, ItemDefinitionId itemDefinitionId, int count)
        {
            for (int i = 0; i < results.Count; i++)
            {
                LootItemQuantity current = results[i];
                if (!current.ItemDefinitionId.Equals(itemDefinitionId))
                    continue;

                long merged = (long)current.Count + count;
                results[i] = new LootItemQuantity(itemDefinitionId, merged > int.MaxValue ? int.MaxValue : (int)merged);
                return;
            }

            results.Add(new LootItemQuantity(itemDefinitionId, count));
        }

        private static int CompareLootItems(LootItemQuantity left, LootItemQuantity right)
        {
            return left.ItemDefinitionId.Value.CompareTo(right.ItemDefinitionId.Value);
        }
    }
}

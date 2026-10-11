using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MxFramework.Runtime;

namespace MxFramework.Gameplay
{
    public enum GameplayBuffPresentationPolarity
    {
        Neutral = 0,
        Buff = 1,
        Debuff = 2,
        Aura = 3
    }

    public enum GameplayBuffPresentationSurface
    {
        PlayerHud = 0,
        TargetHud = 1,
        HeadPad = 2
    }

    public readonly struct GameplayBuffPresentationDefinition :
        IEquatable<GameplayBuffPresentationDefinition>
    {
        public GameplayBuffPresentationDefinition(
            int buffId,
            string displayName,
            string shortName,
            string description,
            string iconResourceKey,
            GameplayBuffPresentationPolarity polarity,
            int priority,
            bool showOnPlayerHud,
            bool showOnTargetHud,
            bool showOnHeadPad,
            string effectSummary)
            : this(
                buffId,
                displayName,
                string.Empty,
                shortName,
                description,
                "Manual",
                iconResourceKey,
                polarity,
                priority,
                showOnPlayerHud,
                showOnTargetHud,
                showOnHeadPad,
                effectSummary)
        {
        }

        public GameplayBuffPresentationDefinition(
            int buffId,
            string displayName,
            string nameKey,
            string shortName,
            string description,
            string descriptionMode,
            string iconResourceKey,
            GameplayBuffPresentationPolarity polarity,
            int priority,
            bool showOnPlayerHud,
            bool showOnTargetHud,
            bool showOnHeadPad,
            string effectSummary)
        {
            if (buffId <= 0)
                throw new ArgumentOutOfRangeException(nameof(buffId), "Buff presentation buff id must be positive.");
            if (!Enum.IsDefined(typeof(GameplayBuffPresentationPolarity), polarity))
                throw new ArgumentOutOfRangeException(nameof(polarity), "Buff presentation polarity is unsupported.");

            BuffId = buffId;
            DisplayName = displayName ?? string.Empty;
            NameKey = nameKey ?? string.Empty;
            ShortName = shortName ?? string.Empty;
            Description = description ?? string.Empty;
            DescriptionMode = string.IsNullOrWhiteSpace(descriptionMode) ? "Manual" : descriptionMode;
            IconResourceKey = iconResourceKey ?? string.Empty;
            Polarity = polarity;
            Priority = priority;
            ShowOnPlayerHud = showOnPlayerHud;
            ShowOnTargetHud = showOnTargetHud;
            ShowOnHeadPad = showOnHeadPad;
            EffectSummary = effectSummary ?? string.Empty;
        }

        public int BuffId { get; }
        public string DisplayName { get; }
        public string NameKey { get; }
        public string ShortName { get; }
        public string Description { get; }
        public string DescriptionMode { get; }
        public string IconResourceKey { get; }
        public GameplayBuffPresentationPolarity Polarity { get; }
        public int Priority { get; }
        public bool ShowOnPlayerHud { get; }
        public bool ShowOnTargetHud { get; }
        public bool ShowOnHeadPad { get; }
        public string EffectSummary { get; }

        public bool IsVisibleOn(GameplayBuffPresentationSurface surface)
        {
            switch (surface)
            {
                case GameplayBuffPresentationSurface.PlayerHud:
                    return ShowOnPlayerHud;
                case GameplayBuffPresentationSurface.TargetHud:
                    return ShowOnTargetHud;
                case GameplayBuffPresentationSurface.HeadPad:
                    return ShowOnHeadPad;
                default:
                    return false;
            }
        }

        public bool Equals(GameplayBuffPresentationDefinition other)
        {
            return BuffId == other.BuffId
                && string.Equals(DisplayName, other.DisplayName, StringComparison.Ordinal)
                && string.Equals(NameKey, other.NameKey, StringComparison.Ordinal)
                && string.Equals(ShortName, other.ShortName, StringComparison.Ordinal)
                && string.Equals(Description, other.Description, StringComparison.Ordinal)
                && string.Equals(DescriptionMode, other.DescriptionMode, StringComparison.Ordinal)
                && string.Equals(IconResourceKey, other.IconResourceKey, StringComparison.Ordinal)
                && Polarity == other.Polarity
                && Priority == other.Priority
                && ShowOnPlayerHud == other.ShowOnPlayerHud
                && ShowOnTargetHud == other.ShowOnTargetHud
                && ShowOnHeadPad == other.ShowOnHeadPad
                && string.Equals(EffectSummary, other.EffectSummary, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is GameplayBuffPresentationDefinition other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = BuffId;
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(DisplayName);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(NameKey);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(ShortName);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(Description);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(DescriptionMode);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(IconResourceKey);
                hash = (hash * 397) ^ (int)Polarity;
                hash = (hash * 397) ^ Priority;
                hash = (hash * 397) ^ ShowOnPlayerHud.GetHashCode();
                hash = (hash * 397) ^ ShowOnTargetHud.GetHashCode();
                hash = (hash * 397) ^ ShowOnHeadPad.GetHashCode();
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(EffectSummary);
                return hash;
            }
        }
    }

    public sealed class GameplayBuffPresentationDefinitionRegistry
    {
        private readonly GameplayBuffPresentationDefinition[] _definitions;

        public GameplayBuffPresentationDefinitionRegistry(
            params GameplayBuffPresentationDefinition[] definitions)
        {
            _definitions = CopySorted(definitions);
        }

        public int Count => _definitions.Length;

        public bool TryGet(int buffId, out GameplayBuffPresentationDefinition definition)
        {
            if (buffId <= 0)
            {
                definition = default;
                return false;
            }

            int left = 0;
            int right = _definitions.Length - 1;
            while (left <= right)
            {
                int mid = left + ((right - left) / 2);
                int id = _definitions[mid].BuffId;
                if (id == buffId)
                {
                    definition = _definitions[mid];
                    return true;
                }

                if (id < buffId)
                    left = mid + 1;
                else
                    right = mid - 1;
            }

            definition = default;
            return false;
        }

        public GameplayBuffPresentationDefinition[] ToArray()
        {
            if (_definitions.Length == 0)
                return Array.Empty<GameplayBuffPresentationDefinition>();

            var copy = new GameplayBuffPresentationDefinition[_definitions.Length];
            Array.Copy(_definitions, copy, _definitions.Length);
            return copy;
        }

        private static GameplayBuffPresentationDefinition[] CopySorted(
            GameplayBuffPresentationDefinition[] definitions)
        {
            if (definitions == null || definitions.Length == 0)
                return Array.Empty<GameplayBuffPresentationDefinition>();

            var copy = new GameplayBuffPresentationDefinition[definitions.Length];
            Array.Copy(definitions, copy, definitions.Length);
            Array.Sort(copy, CompareDefinitions);
            for (int i = 0; i < copy.Length; i++)
            {
                if (copy[i].BuffId <= 0)
                    throw new ArgumentException("Buff presentation registry cannot contain default definitions.", nameof(definitions));
                if (i > 0 && copy[i - 1].BuffId == copy[i].BuffId)
                    throw new ArgumentException("Buff presentation registry cannot contain duplicate buff ids.", nameof(definitions));
            }

            return copy;
        }

        private static int CompareDefinitions(
            GameplayBuffPresentationDefinition left,
            GameplayBuffPresentationDefinition right)
        {
            return left.BuffId.CompareTo(right.BuffId);
        }
    }

    public readonly struct GameplayBuffPresentationItem :
        IEquatable<GameplayBuffPresentationItem>
    {
        public GameplayBuffPresentationItem(
            int buffId,
            string displayName,
            string shortName,
            int stackCount,
            long remainingFrames,
            float remaining01,
            bool isPermanent,
            GameplayBuffPresentationPolarity polarity,
            string iconKey,
            string effectSummary,
            int priority)
            : this(
                buffId,
                displayName,
                string.Empty,
                shortName,
                string.Empty,
                "Manual",
                stackCount,
                remainingFrames,
                remaining01,
                isPermanent,
                polarity,
                iconKey,
                effectSummary,
                priority)
        {
        }

        public GameplayBuffPresentationItem(
            int buffId,
            string displayName,
            string nameKey,
            string shortName,
            string description,
            string descriptionMode,
            int stackCount,
            long remainingFrames,
            float remaining01,
            bool isPermanent,
            GameplayBuffPresentationPolarity polarity,
            string iconKey,
            string effectSummary,
            int priority)
        {
            BuffId = buffId;
            DisplayName = displayName ?? string.Empty;
            NameKey = nameKey ?? string.Empty;
            ShortName = shortName ?? string.Empty;
            Description = description ?? string.Empty;
            DescriptionMode = string.IsNullOrWhiteSpace(descriptionMode) ? "Manual" : descriptionMode;
            StackCount = stackCount;
            RemainingFrames = remainingFrames < 0L ? 0L : remainingFrames;
            Remaining01 = Clamp01(remaining01);
            IsPermanent = isPermanent;
            Polarity = polarity;
            IconKey = iconKey ?? string.Empty;
            EffectSummary = effectSummary ?? string.Empty;
            Priority = priority;
        }

        public int BuffId { get; }
        public string DisplayName { get; }
        public string NameKey { get; }
        public string ShortName { get; }
        public string Description { get; }
        public string DescriptionMode { get; }
        public int StackCount { get; }
        public long RemainingFrames { get; }
        public float Remaining01 { get; }
        public bool IsPermanent { get; }
        public GameplayBuffPresentationPolarity Polarity { get; }
        public string IconKey { get; }
        public string EffectSummary { get; }
        public int Priority { get; }

        public bool Equals(GameplayBuffPresentationItem other)
        {
            return BuffId == other.BuffId
                && string.Equals(DisplayName, other.DisplayName, StringComparison.Ordinal)
                && string.Equals(NameKey, other.NameKey, StringComparison.Ordinal)
                && string.Equals(ShortName, other.ShortName, StringComparison.Ordinal)
                && string.Equals(Description, other.Description, StringComparison.Ordinal)
                && string.Equals(DescriptionMode, other.DescriptionMode, StringComparison.Ordinal)
                && StackCount == other.StackCount
                && RemainingFrames == other.RemainingFrames
                && Math.Abs(Remaining01 - other.Remaining01) < 0.0001f
                && IsPermanent == other.IsPermanent
                && Polarity == other.Polarity
                && string.Equals(IconKey, other.IconKey, StringComparison.Ordinal)
                && string.Equals(EffectSummary, other.EffectSummary, StringComparison.Ordinal)
                && Priority == other.Priority;
        }

        public override bool Equals(object obj)
        {
            return obj is GameplayBuffPresentationItem other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = BuffId;
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(DisplayName);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(NameKey);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(ShortName);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(Description);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(DescriptionMode);
                hash = (hash * 397) ^ StackCount;
                hash = (hash * 397) ^ RemainingFrames.GetHashCode();
                hash = (hash * 397) ^ Remaining01.GetHashCode();
                hash = (hash * 397) ^ IsPermanent.GetHashCode();
                hash = (hash * 397) ^ (int)Polarity;
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(IconKey);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(EffectSummary);
                hash = (hash * 397) ^ Priority;
                return hash;
            }
        }

        private static float Clamp01(float value)
        {
            if (value <= 0f)
                return 0f;
            return value >= 1f ? 1f : value;
        }
    }

    public static class GameplayBuffPresentationSnapshotBuilder
    {
        /// <summary>
        /// Builds presentation rows into caller-owned buffers.
        /// </summary>
        /// <remarks>
        /// NoAlloc after initialization when <paramref name="output"/> and <paramref name="scratchEntries"/>
        /// have enough capacity and presentation definitions avoid fallback string generation.
        /// Invalid-call and fallback-metadata paths may allocate diagnostics or display strings.
        /// </remarks>
        public static int BuildInto(
            GameplayComponentWorld world,
            GameplayEntityId entityId,
            GameplayBuffDefinitionRegistry buffDefinitions,
            GameplayBuffPresentationDefinitionRegistry presentationDefinitions,
            RuntimeFrame currentFrame,
            GameplayBuffPresentationSurface surface,
            List<GameplayBuffPresentationItem> output,
            List<GameplayComponentBuffEntry> scratchEntries,
            int maxItems = 0)
        {
            if (output == null)
                throw new ArgumentNullException(nameof(output));
            if (scratchEntries == null)
                throw new ArgumentNullException(nameof(scratchEntries));

            output.Clear();
            scratchEntries.Clear();
            if (world == null
                || !entityId.IsValid
                || buffDefinitions == null
                || !world.TryGetStore(out GameplayComponentStore<GameplayComponentBuffSetComponent> store))
            {
                return 0;
            }

            if (!store.TryGet(entityId, out GameplayComponentBuffSetComponent buffSet) || buffSet.Count == 0)
                return 0;

            buffSet.CopyTo(scratchEntries);
            for (int i = 0; i < scratchEntries.Count; i++)
            {
                GameplayComponentBuffEntry entry = scratchEntries[i];
                if (!buffDefinitions.TryGet(entry.BuffId, out GameplayBuffDefinition definition))
                    continue;

                GameplayBuffPresentationDefinition presentation = default;
                bool hasPresentation = presentationDefinitions != null
                    && presentationDefinitions.TryGet(entry.BuffId, out presentation);
                if (!hasPresentation)
                    presentation = CreateFallbackPresentation(definition);
                if (!presentation.IsVisibleOn(surface))
                    continue;

                long remainingFrames = entry.IsPermanent
                    ? 0L
                    : Math.Max(0L, entry.EndFrame - currentFrame.Value);
                float remaining01 = entry.IsPermanent
                    ? 1f
                    : definition.DefaultDurationFrames > 0L
                        ? Clamp01((float)remainingFrames / definition.DefaultDurationFrames)
                        : 0f;

                output.Add(new GameplayBuffPresentationItem(
                    entry.BuffId,
                    !string.IsNullOrWhiteSpace(presentation.DisplayName)
                        ? presentation.DisplayName
                        : definition.StableId,
                    presentation.NameKey,
                    !string.IsNullOrWhiteSpace(presentation.ShortName)
                        ? presentation.ShortName
                        : BuildShortName(definition.StableId, entry.BuffId),
                    presentation.Description,
                    presentation.DescriptionMode,
                    entry.StackCount,
                    remainingFrames,
                    remaining01,
                    entry.IsPermanent,
                    presentation.Polarity,
                    presentation.IconResourceKey,
                    !string.IsNullOrWhiteSpace(presentation.EffectSummary)
                        ? presentation.EffectSummary
                        : FormatEffects(definition.GetEffects()),
                    presentation.Priority));
            }

            if (output.Count == 0)
                return 0;

            output.Sort(GameplayBuffPresentationItemComparer.Instance);
            if (maxItems > 0 && output.Count > maxItems)
                output.RemoveRange(maxItems, output.Count - maxItems);
            return output.Count;
        }

        public static GameplayBuffPresentationItem[] Build(
            GameplayComponentWorld world,
            GameplayEntityId entityId,
            GameplayBuffDefinitionRegistry buffDefinitions,
            GameplayBuffPresentationDefinitionRegistry presentationDefinitions,
            RuntimeFrame currentFrame,
            GameplayBuffPresentationSurface surface,
            int maxItems = 0)
        {
            if (world == null
                || !entityId.IsValid
                || buffDefinitions == null
                || !world.TryGetStore(out GameplayComponentStore<GameplayComponentBuffSetComponent> store))
            {
                return Array.Empty<GameplayBuffPresentationItem>();
            }

            if (!store.TryGet(entityId, out GameplayComponentBuffSetComponent buffSet) || buffSet.Count == 0)
                return Array.Empty<GameplayBuffPresentationItem>();

            var entries = new List<GameplayComponentBuffEntry>(buffSet.Count);
            var items = new List<GameplayBuffPresentationItem>(buffSet.Count);
            BuildInto(
                world,
                entityId,
                buffDefinitions,
                presentationDefinitions,
                currentFrame,
                surface,
                items,
                entries,
                maxItems);
            if (items.Count == 0)
                return Array.Empty<GameplayBuffPresentationItem>();
            return items.ToArray();
        }

        public static GameplayBuffPresentationDefinition CreateFallbackPresentation(
            GameplayBuffDefinition definition)
        {
            string displayName = !string.IsNullOrWhiteSpace(definition.StableId)
                ? definition.StableId
                : "Buff " + definition.BuffId.ToString(CultureInfo.InvariantCulture);
            GameplayBuffPresentationPolarity polarity = InferPolarity(definition.GetEffects());
            return new GameplayBuffPresentationDefinition(
                definition.BuffId,
                displayName,
                string.Empty,
                BuildShortName(displayName, definition.BuffId),
                string.Empty,
                "Auto",
                string.Empty,
                polarity,
                DefaultPriority(polarity),
                showOnPlayerHud: true,
                showOnTargetHud: true,
                showOnHeadPad: true,
                FormatEffects(definition.GetEffects()));
        }

        public static string FormatEffects(IReadOnlyList<GameplayBuffEffectDefinition> effects)
        {
            if (effects == null || effects.Count == 0)
                return string.Empty;

            var builder = new StringBuilder();
            for (int i = 0; i < effects.Count; i++)
            {
                if (i > 0)
                    builder.Append("; ");

                GameplayBuffEffectDefinition effect = effects[i];
                switch (effect.Kind)
                {
                    case GameplayBuffEffectKind.AttributeModifier:
                        builder.Append(FormatAttribute(effect.AttributeId))
                            .Append(' ')
                            .Append(effect.AddValue >= 0 ? "+" : string.Empty)
                            .Append(effect.AddValue.ToString(CultureInfo.InvariantCulture));
                        break;
                    case GameplayBuffEffectKind.Status:
                        builder.Append("Status(")
                            .Append(effect.StatusId.Value.ToString(CultureInfo.InvariantCulture))
                            .Append(')');
                        break;
                    default:
                        builder.Append("Effect(")
                            .Append(effect.EffectId.ToString(CultureInfo.InvariantCulture))
                            .Append(')');
                        break;
                }
            }

            return builder.ToString();
        }

        public static GameplayBuffPresentationPolarity InferPolarity(
            IReadOnlyList<GameplayBuffEffectDefinition> effects)
        {
            bool hasPositive = false;
            bool hasNegative = false;
            for (int i = 0; effects != null && i < effects.Count; i++)
            {
                GameplayBuffEffectDefinition effect = effects[i];
                if (effect.Kind != GameplayBuffEffectKind.AttributeModifier || effect.AddValue == 0)
                    continue;

                hasPositive |= effect.AddValue > 0;
                hasNegative |= effect.AddValue < 0;
            }

            if (hasPositive && !hasNegative)
                return GameplayBuffPresentationPolarity.Buff;
            if (hasNegative && !hasPositive)
                return GameplayBuffPresentationPolarity.Debuff;
            return GameplayBuffPresentationPolarity.Neutral;
        }

        public static int DefaultPriority(GameplayBuffPresentationPolarity polarity)
        {
            switch (polarity)
            {
                case GameplayBuffPresentationPolarity.Debuff:
                    return 200;
                case GameplayBuffPresentationPolarity.Aura:
                    return 150;
                case GameplayBuffPresentationPolarity.Buff:
                    return 100;
                default:
                    return 0;
            }
        }

        public static string BuildShortName(string value, int fallbackId)
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallbackId.ToString(CultureInfo.InvariantCulture);

            var builder = new StringBuilder(4);
            bool takeNext = true;
            for (int i = 0; i < value.Length && builder.Length < 4; i++)
            {
                char c = value[i];
                if (!char.IsLetterOrDigit(c))
                {
                    takeNext = true;
                    continue;
                }

                if (takeNext || char.IsUpper(c))
                {
                    builder.Append(char.ToUpperInvariant(c));
                    takeNext = false;
                }
            }

            if (builder.Length == 0)
            {
                for (int i = 0; i < value.Length && builder.Length < 4; i++)
                {
                    char c = value[i];
                    if (char.IsLetterOrDigit(c))
                        builder.Append(char.ToUpperInvariant(c));
                }
            }

            return builder.Length > 0
                ? builder.ToString()
                : fallbackId.ToString(CultureInfo.InvariantCulture);
        }

        private sealed class GameplayBuffPresentationItemComparer :
            IComparer<GameplayBuffPresentationItem>
        {
            public static readonly GameplayBuffPresentationItemComparer Instance =
                new GameplayBuffPresentationItemComparer();

            public int Compare(GameplayBuffPresentationItem left, GameplayBuffPresentationItem right)
            {
                int priority = right.Priority.CompareTo(left.Priority);
                if (priority != 0)
                    return priority;

                int polarity = right.Polarity.CompareTo(left.Polarity);
                if (polarity != 0)
                    return polarity;

                return left.BuffId.CompareTo(right.BuffId);
            }
        }

        private static string FormatAttribute(int attributeId)
        {
            switch (attributeId)
            {
                case 1:
                    return "HP(1)";
                case 2:
                    return "Attack(2)";
                case 3:
                    return "Defense(3)";
                case 4:
                    return "ScatterPressure(4)";
                default:
                    return "Attribute(" + attributeId.ToString(CultureInfo.InvariantCulture) + ")";
            }
        }

        private static float Clamp01(float value)
        {
            if (value <= 0f)
                return 0f;
            return value >= 1f ? 1f : value;
        }
    }
}

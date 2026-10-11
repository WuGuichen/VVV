using System;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>Presentation state only; no authored rarity or acquisition tier is currently configured.</summary>
    internal enum TacticalCardQualityPresentation { Unspecified }

    /// <summary>Read-only remaining bearing capacity derived from the current authoritative combat facts.</summary>
    internal readonly struct TacticalCohesionReadout
    {
        private TacticalCohesionReadout(bool available, bool broken, float remaining01)
        {
            IsAvailable = available;
            IsBroken = broken;
            Remaining01 = remaining01;
        }

        public bool IsAvailable { get; }
        public bool IsBroken { get; }
        public float Remaining01 { get; }

        public static TacticalCohesionReadout FromScatterPressure(int scatter, int currentLimit, bool broken)
        {
            if (scatter < 0 || currentLimit <= 0)
                return new TacticalCohesionReadout(false, broken, 0f);
            float remaining = broken ? 0f : Math.Max(0f, Math.Min(1f, 1f - (float)scatter / currentLimit));
            return new TacticalCohesionReadout(true, broken, remaining);
        }
    }

    /// <summary>Card display values preserve the frozen published identity and never own card transactions.</summary>
    internal readonly struct TacticalCardDisplayData
    {
        private TacticalCardDisplayData(TacticalCardDefinition definition)
        {
            IsAvailable = definition != null;
            Name = definition?.Name ?? string.Empty;
            Description = definition?.Description ?? string.Empty;
            Cost = definition?.BaseMomentumCost ?? 0;
            Kind = definition?.Kind ?? default;
            var action = definition;
            IconKey = action?.IconKey ?? string.Empty;
            PublishedCardId = action?.PublishedCardId ?? string.Empty;
            PublishedRevision = action?.PublishedRevision ?? 0;
            PublishedContentHash = action?.PublishedContentHash ?? string.Empty;
            DeckEntryInstanceId = action?.DeckEntryInstanceId ?? 0;
        }

        public bool IsAvailable { get; }
        public string Name { get; }
        public string Description { get; }
        public int Cost { get; }
        public TacticalCardKind Kind { get; }
        public string IconKey { get; }
        public bool HasAuthoredIcon => !string.IsNullOrWhiteSpace(IconKey);
        public TacticalCardQualityPresentation Quality => TacticalCardQualityPresentation.Unspecified;
        public string PublishedCardId { get; }
        public int PublishedRevision { get; }
        public string PublishedContentHash { get; }
        public ulong DeckEntryInstanceId { get; }

        public static TacticalCardDisplayData FromDefinition(TacticalCardDefinition definition) =>
            new TacticalCardDisplayData(definition);
    }

    /// <summary>View-only copy of the existing tactical ledger; initial motion comes from the bound stage configuration.</summary>
    internal readonly struct TacticalMotionReadout
    {
        private TacticalMotionReadout(bool available, int initial, int current, int reserved, int spendable)
        {
            IsAvailable = available;
            Initial = initial;
            Current = current;
            Reserved = reserved;
            Spendable = spendable;
        }

        public bool IsAvailable { get; }
        public int Initial { get; }
        public int Capacity => MomentumLedger.Maximum;
        public int Current { get; }
        public int Reserved { get; }
        public int Spendable { get; }

        public static TacticalMotionReadout FromLedger(IMomentumLedger ledger, int configuredInitial)
        {
            if (ledger == null) return default;
            int current = ledger.Current, reserved = ledger.Reserved, spendable = ledger.Spendable;
            if (configuredInitial < 0 || configuredInitial > MomentumLedger.Maximum
                || current < 0 || current > MomentumLedger.Maximum || reserved < 0 || reserved > current
                || spendable != current - reserved)
                return default;
            return new TacticalMotionReadout(true, configuredInitial, current, reserved, spendable);
        }
    }
}

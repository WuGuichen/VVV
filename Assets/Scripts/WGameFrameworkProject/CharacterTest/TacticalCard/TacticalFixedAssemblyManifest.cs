using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using MxFramework.Runtime;
using Newtonsoft.Json;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    internal enum TacticalAccessoryLifetimeKind { Unspecified, UntilBattleEnd, LimitedTurns }

    /// <summary>Author-owned v2 parameters. No fee, lifetime or balance default enables execution.</summary>
    [JsonObject(MissingMemberHandling = MissingMemberHandling.Error)]
    internal sealed class TacticalAccessoryExecutionDefinition
    {
        public int Version { get; set; }
        public TacticalAccessoryCombatKind Kind { get; set; }
        public TacticalAccessoryLifetimeKind Lifetime { get; set; }
        public int? DurationTurns { get; set; }
        public int? ConfirmationMomentumCost { get; set; }
        public int? MomentumAmount { get; set; }
        public int? ReferenceSpeed { get; set; }
        public int? ReferenceMass { get; set; }
        public int? MinimumMassScale { get; set; }
        public int? MaximumMassScale { get; set; }
        public int? MaximumSpeed { get; set; }

        [JsonIgnore] internal bool IsValid
        {
            get
            {
                if (Version != 1 || !ConfirmationMomentumCost.HasValue || ConfirmationMomentumCost.Value < 0
                    || !DurationTurns.HasValue || !MomentumAmount.HasValue || !ReferenceSpeed.HasValue
                    || !ReferenceMass.HasValue || !MinimumMassScale.HasValue || !MaximumMassScale.HasValue
                    || !MaximumSpeed.HasValue
                    || (Lifetime == TacticalAccessoryLifetimeKind.UntilBattleEnd ? DurationTurns.Value != 0
                        : Lifetime != TacticalAccessoryLifetimeKind.LimitedTurns || DurationTurns.Value <= 0)) return false;
                if (Kind == TacticalAccessoryCombatKind.PushAway)
                    return MomentumAmount.Value == 0 && Push.IsValid;
                return (Kind == TacticalAccessoryCombatKind.BorrowMomentum || Kind == TacticalAccessoryCombatKind.BreakMomentum)
                    && MomentumAmount.Value > 0 && ReferenceSpeed.Value == 0 && ReferenceMass.Value == 0
                    && MinimumMassScale.Value == 0 && MaximumMassScale.Value == 0 && MaximumSpeed.Value == 0;
            }
        }

        [JsonIgnore] internal TacticalAccessoryPushParameters Push => new TacticalAccessoryPushParameters(
            ReferenceSpeed.GetValueOrDefault(), ReferenceMass.GetValueOrDefault(), MinimumMassScale.GetValueOrDefault(),
            MaximumMassScale.GetValueOrDefault(), MaximumSpeed.GetValueOrDefault());
    }

    /// <summary>Private value copy of one installed exact publication. UI slot does not determine execution order.</summary>
    internal sealed class TacticalFixedAccessoryManifest
    {
        internal TacticalFixedAccessoryManifest(CharacterTacticalAccessorySlot slot, CharacterTacticalAccessoryDefinition definition)
        {
            Slot = slot.SlotIndex; ItemInstance = slot.AccessoryItemInstanceId; ItemDefinition = definition.ItemDefinitionId;
            AccessoryId = definition.Reference.AccessoryId; Revision = definition.Reference.Revision;
            ContentHash = definition.Reference.ContentHash; Quality = definition.Quality;
            TriggerContract = definition.TriggerContractId; FailureContract = definition.FailureContractId;
            LifetimeContract = definition.LifetimeContractId;
            var execution = definition.Execution;
            ExecutionVersion = execution.Version; Kind = execution.Kind; Lifetime = execution.Lifetime;
            DurationTurns = execution.DurationTurns.Value; ConfirmationCost = execution.ConfirmationMomentumCost.Value;
            MomentumAmount = execution.MomentumAmount.Value; Push = execution.Push;
        }
        internal int Slot { get; }
        internal int ItemInstance { get; }
        internal int ItemDefinition { get; }
        internal string AccessoryId { get; }
        internal int Revision { get; }
        internal string ContentHash { get; }
        internal CharacterTacticalCardQuality Quality { get; }
        internal string TriggerContract { get; }
        internal string FailureContract { get; }
        internal string LifetimeContract { get; }
        internal int ExecutionVersion { get; }
        internal TacticalAccessoryCombatKind Kind { get; }
        internal TacticalAccessoryLifetimeKind Lifetime { get; }
        internal int DurationTurns { get; }
        internal int ConfirmationCost { get; }
        internal int MomentumAmount { get; }
        internal TacticalAccessoryPushParameters Push { get; }

        internal bool TryGetLastTurn(ulong firstTurn, out ulong lastTurn)
        {
            lastTurn = 0;
            if (firstTurn == 0) return false;
            if (Lifetime == TacticalAccessoryLifetimeKind.UntilBattleEnd) { lastTurn = ulong.MaxValue; return true; }
            ulong offset = checked((ulong)DurationTurns - 1);
            if (firstTurn > ulong.MaxValue - offset) return false;
            lastTurn = firstTurn + offset; return true;
        }

        internal object CanonicalData() => new { Slot, ItemInstance, ItemDefinition, AccessoryId, Revision, ContentHash, Quality,
            TriggerContract, FailureContract, LifetimeContract, ExecutionVersion, Kind, Lifetime, DurationTurns, ConfirmationCost,
            MomentumAmount, ReferenceSpeed = Push.ReferenceSpeedMillimetersPerSecond, ReferenceMass = Push.ReferenceMassGrams,
            MinimumMassScale = Push.MinimumMassScaleMilli, MaximumMassScale = Push.MaximumMassScaleMilli,
            MaximumSpeed = Push.MaximumSpeedMillimetersPerSecond };
    }

    /// <summary>One held card's immutable deployment contract. Does not grant ownership or admit a play.</summary>
    internal sealed class TacticalFixedAssemblyManifest
    {
        internal const int FormatVersion = 1, OrderingVersion = 1;
        private readonly ReadOnlyCollection<TacticalFixedAccessoryManifest> _accessories;
        private readonly ReadOnlyDictionary<string, int> _bindings;
        private TacticalFixedAssemblyManifest(CharacterTacticalAssemblySnapshot snapshot, TacticalCardDefinition battleDefinition,
            int characterItemInstance, int inventoryOwner, long characterRevision,
            IReadOnlyDictionary<string, int> bindings, TacticalFixedAccessoryManifest[] accessories)
        {
            CharacterItemInstance = characterItemInstance; InventoryOwner = inventoryOwner; CharacterRevision = characterRevision;
            ConfigurationRevision = snapshot.Assembly.ConfigurationRevision; ConfigurationHash = snapshot.Assembly.ConfigurationHash;
            HeldCardInstance = snapshot.Assembly.MainCard.InstanceId; BattleEntryInstance = battleDefinition.DeckEntryInstanceId;
            CardId = battleDefinition.PublishedCardId; CardRevision = battleDefinition.PublishedRevision;
            CardContentHash = battleDefinition.PublishedContentHash; Quality = snapshot.Quality;
            SlotCount = snapshot.AccessorySlotCount; MainConfirmationCost = battleDefinition.BaseMomentumCost;
            _accessories = Array.AsReadOnly(accessories);
            _bindings = new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(bindings, StringComparer.Ordinal));
            AccessoryConfirmationCost = checked(accessories.Sum(value => value.ConfirmationCost));
            var canonical = new { FormatVersion, OrderingVersion, CharacterItemInstance, InventoryOwner, CharacterRevision,
                ConfigurationRevision, ConfigurationHash, HeldCardInstance, BattleEntryInstance, CardId, CardRevision,
                CardContentHash, Quality, SlotCount, MainConfirmationCost, AccessoryConfirmationCost,
                Bindings = _bindings.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => new { Slot = pair.Key, Action = pair.Value }).ToArray(),
                Accessories = accessories.Select(value => value.CanonicalData()).ToArray() };
            Digest = RuntimeHashDigest.ComputeContentDigest(JsonConvert.SerializeObject(canonical));
            Fingerprint = Digest.Word0.ToString("x16") + Digest.Word1.ToString("x16")
                + Digest.Word2.ToString("x16") + Digest.Word3.ToString("x16");
        }

        internal int CharacterItemInstance { get; }
        internal int InventoryOwner { get; }
        internal long CharacterRevision { get; }
        internal int ConfigurationRevision { get; }
        internal string ConfigurationHash { get; }
        internal ulong HeldCardInstance { get; }
        internal ulong BattleEntryInstance { get; }
        internal string CardId { get; }
        internal int CardRevision { get; }
        internal string CardContentHash { get; }
        internal CharacterTacticalCardQuality Quality { get; }
        internal int SlotCount { get; }
        internal int MainConfirmationCost { get; }
        internal int AccessoryConfirmationCost { get; }
        internal IReadOnlyList<TacticalFixedAccessoryManifest> Accessories => _accessories;
        internal IReadOnlyDictionary<string, int> Bindings => _bindings;
        internal RuntimeHashDigest Digest { get; }
        internal string Fingerprint { get; }

        /// <remarks>LowFreqAlloc. Input is a trusted owner's detached P1 snapshot; this validates and takes value copies.
        /// Full executable configuration validation and real inventory ownership remain the player's authority.</remarks>
        internal static bool TryCreate(CharacterTacticalAssemblySnapshot snapshot, TacticalCardDefinition battleDefinition,
            int characterItemInstance, int inventoryOwner, long characterRevision, IReadOnlyDictionary<string, int> bindings,
            out TacticalFixedAssemblyManifest manifest, out string issue)
        {
            manifest = null; issue = "fixed-assembly:InvalidFrozenInput";
            var assembly = snapshot?.Assembly; var main = assembly?.MainCard;
            if (snapshot == null || snapshot.Mode != CharacterTacticalAssemblyMode.FixedPreassembled
                || assembly?.Schema != CharacterTacticalCardAssembly.CurrentSchema || assembly.ConfigurationRevision <= 0
                || string.IsNullOrWhiteSpace(assembly.ConfigurationHash) || main?.InstanceId == 0 || main == null
                || characterItemInstance <= 0 || inventoryOwner <= 0 || characterRevision <= 0 || bindings == null
                || !(battleDefinition is TacticalActionCardDefinition) || !battleDefinition.HasPublishedBehavior
                || battleDefinition.DeckEntryInstanceId == 0 || battleDefinition.PublishedCardId != main.CardId
                || battleDefinition.PublishedRevision != main.Revision || battleDefinition.PublishedContentHash != main.ContentHash
                || assembly.Accessories == null || snapshot.Accessories == null) return false;
            try
            {
                if (snapshot.AccessorySlotCount != CharacterTacticalAssemblyRules.SlotCount(snapshot.Quality)
                    || assembly.Accessories.Count > snapshot.AccessorySlotCount || snapshot.Accessories.Length != assembly.Accessories.Count
                    || bindings.Any(pair => string.IsNullOrWhiteSpace(pair.Key) || pair.Value <= 0)) return false;
                var items = new HashSet<int>(); var slots = new HashSet<int>(); var names = new HashSet<string>(StringComparer.Ordinal);
                var frozen = new List<TacticalFixedAccessoryManifest>(assembly.Accessories.Count);
                foreach (var slot in assembly.Accessories)
                {
                    if (slot == null || slot.SlotIndex < 0 || slot.SlotIndex >= snapshot.AccessorySlotCount
                        || !slots.Add(slot.SlotIndex) || slot.AccessoryItemInstanceId <= 0 || !items.Add(slot.AccessoryItemInstanceId)
                        || !CharacterTacticalAssemblyRules.Valid(slot.Reference) || !names.Add(slot.Reference.AccessoryId)) return false;
                    var matches = snapshot.Accessories.Where(value => value != null
                        && CharacterTacticalAssemblyRules.Same(value.Reference, slot.Reference)).ToArray();
                    if (matches.Length != 1 || matches[0].Execution?.IsValid != true || matches[0].ItemDefinitionId <= 0
                        || string.IsNullOrWhiteSpace(matches[0].TriggerContractId) || string.IsNullOrWhiteSpace(matches[0].FailureContractId)
                        || string.IsNullOrWhiteSpace(matches[0].LifetimeContractId)) return false;
                    CharacterTacticalAssemblyRules.SlotCount(matches[0].Quality);
                    frozen.Add(new TacticalFixedAccessoryManifest(slot, matches[0]));
                }
                // Stable mechanism and exact publication identity order, independent of UI slot/collection order.
                var ordered = frozen.OrderBy(value => (int)value.Kind).ThenBy(value => value.AccessoryId, StringComparer.Ordinal)
                    .ThenBy(value => value.Revision).ThenBy(value => value.ContentHash, StringComparer.Ordinal)
                    .ThenBy(value => value.ItemInstance).ToArray();
                manifest = new TacticalFixedAssemblyManifest(snapshot, battleDefinition, characterItemInstance,
                    inventoryOwner, characterRevision, bindings, ordered);
                issue = string.Empty; return true;
            }
            catch (Exception error) when (error is ArgumentException || error is OverflowException || error is JsonException)
            { issue = "fixed-assembly:InvalidFrozenInput:" + error.GetType().Name; return false; }
        }
    }
}

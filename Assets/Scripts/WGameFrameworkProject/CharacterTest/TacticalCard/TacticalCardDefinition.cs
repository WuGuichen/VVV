using System;
using MxFramework.CharacterApplication;
using MxFramework.CharacterAction;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>Immutable battle configuration. Construction is a cold, allocating path.</summary>
    public abstract class TacticalCardDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public abstract TacticalCardKind Kind { get; }
        public string PublishedCardId { get; protected set; } = string.Empty;
        public int PublishedRevision { get; protected set; }
        public string PublishedContentHash { get; protected set; } = string.Empty;
        public ulong DeckEntryInstanceId { get; protected set; }
        /// <summary>Presentation key from the exact published card; never an execution or acquisition identity.</summary>
        public string IconKey { get; protected set; } = string.Empty;
        public bool HasPublishedBehavior => !string.IsNullOrEmpty(PublishedCardId);
        internal TacticalFixedAssemblyManifest FixedAssembly { get; private set; }
        /// <summary>LowFreqAlloc. A deployment definition accepts one exact immutable instance assembly.</summary>
        internal void AttachFixedAssembly(TacticalFixedAssemblyManifest manifest)
        {
            if (manifest == null || FixedAssembly != null || manifest.BattleEntryInstance != DeckEntryInstanceId
                || manifest.CardId != PublishedCardId || manifest.CardRevision != PublishedRevision
                || manifest.CardContentHash != PublishedContentHash || manifest.MainConfirmationCost != BaseMomentumCost)
                throw new InvalidOperationException("fixed-assembly:DeploymentDefinitionMismatch");
            FixedAssembly = manifest;
        }
        /// <summary>Nonnegative; costs above 100 remain unaffordable, never discounted.</summary>
        public int BaseMomentumCost { get; }
        public CardDestination DestinationAfterUse { get; }

        protected TacticalCardDefinition(string id, string name, string description,
            int baseMomentumCost, CardDestination destinationAfterUse)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A stable ID is required.", nameof(id));
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A name is required.", nameof(name));
            if (baseMomentumCost < 0) throw new ArgumentOutOfRangeException(nameof(baseMomentumCost));
            if (destinationAfterUse != CardDestination.Discard && destinationAfterUse != CardDestination.Exhaust)
                throw new ArgumentOutOfRangeException(nameof(destinationAfterUse));
            Id = id;
            Name = name;
            Description = description ?? string.Empty;
            BaseMomentumCost = baseMomentumCost;
            DestinationAfterUse = destinationAfterUse;
        }
    }

    /// <summary>
    /// Tactical action card definition. Explicitly identifies the authoritative physical action,
    /// its tactical cost, cooldown, target mode and approach policy.
    /// </summary>
    public sealed partial class TacticalActionCardDefinition : TacticalCardDefinition
    {
        internal TacticalPointMoveRules PointMove { get; private set; }
        internal static TacticalActionCardDefinition CreatePointMove(string id, string name, string description, int cost,
            TacticalPointMoveRules rules, float cooldownSeconds, CardDestination destinationAfterUse)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            if (float.IsNaN(cooldownSeconds) || float.IsInfinity(cooldownSeconds) || cooldownSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(cooldownSeconds), "Legacy cooldown must be finite and non-negative.");
            // Pure point movement has no physical action/Ability owner. Its configured mechanism
            // cooldown starts only when the formal control consumer accepts the first owned move.
            return new TacticalActionCardDefinition(id, name, description, cost, default(CharacterActionId), cooldownSeconds,
                TacticalTargetMode.None, TacticalApproachMode.None, destinationAfterUse: destinationAfterUse) { PointMove = rules };
        }

        internal static TacticalActionCardDefinition CreatePublishedPointMove(string id, string name, string description,
            int cost, TacticalPointMoveRules rules, float cooldownSeconds, string publishedCardId, int revision,
            string contentHash, ulong instanceId, string iconKey)
        {
            if (string.IsNullOrWhiteSpace(publishedCardId) || revision <= 0 || string.IsNullOrWhiteSpace(contentHash))
                throw new ArgumentException("Exact published point reference required.");
            var card = CreatePointMove(id, name, description, cost, rules, cooldownSeconds, CardDestination.Discard);
            card.PublishedCardId = publishedCardId; card.PublishedRevision = revision; card.PublishedContentHash = contentHash;
            card.DeckEntryInstanceId = instanceId; card.IconKey = iconKey ?? string.Empty;
            return card;
        }


        public TacticalActionCardDefinition(string id, string name, string description, int cost,
            string publishedCardId, int revision, string contentHash, ulong deckEntryInstanceId = 0)
            : this(id, name, description, cost, publishedCardId, revision, contentHash, deckEntryInstanceId, 0f) { }

        public TacticalActionCardDefinition(string id, string name, string description, int cost,
            string publishedCardId, int revision, string contentHash, ulong deckEntryInstanceId, float cooldownSeconds)
            : this(id, name, description, cost, default(CharacterActionId), cooldownSeconds)
        {
            if (string.IsNullOrWhiteSpace(publishedCardId) || revision <= 0) throw new ArgumentException("Published card reference required.");
            PublishedCardId = publishedCardId; PublishedRevision = revision; PublishedContentHash = contentHash ?? string.Empty;
            DeckEntryInstanceId = deckEntryInstanceId;
        }
        public TacticalActionCardDefinition(string id, string name, string description, int cost,
            string publishedCardId, int revision, string contentHash, ulong deckEntryInstanceId, string iconKey)
            : this(id, name, description, cost, publishedCardId, revision, contentHash, deckEntryInstanceId, iconKey, 0f) { }

        public TacticalActionCardDefinition(string id, string name, string description, int cost,
            string publishedCardId, int revision, string contentHash, ulong deckEntryInstanceId, string iconKey, float cooldownSeconds)
            : this(id, name, description, cost, publishedCardId, revision, contentHash, deckEntryInstanceId, cooldownSeconds)
        {
            IconKey = iconKey ?? string.Empty;
        }
        public override TacticalCardKind Kind => TacticalCardKind.Action;

        /// <summary>Authoritative physical action identifier.</summary>
        public CharacterActionId ActionId { get; }

        /// <summary>Action slot abstraction for generalized tactical cards.</summary>
        public TacticalActionSlot ActionSlot { get; }

        /// <summary>True if this card specifies a generalized action slot.</summary>
        public bool HasActionSlot => ActionSlot != TacticalActionSlot.None;

        /// <summary>True if this card specifies an explicit physical action.</summary>
        public bool HasActionId => ActionId.IsValid;

        /// <summary>Warning recorded when both ActionId and ActionSlot are configured (FixedActionOverridesSlot).</summary>
        public string ConfigurationWarning { get; }

        /// <summary>Configured mechanism cooldown. Physical actions use their Ability owner's cooldown.</summary>
        public float CooldownSeconds { get; }

        /// <summary>Only the approved pure point-move mechanism uses the legacy card cooldown owner.</summary>
        internal bool UsesCardCooldown => PointMove != null && CooldownSeconds > 0f;

        public TacticalTargetMode TargetMode { get; }
        public TacticalApproachMode Approach { get; }

        /// <summary>Legacy profile identifier; eliminated in Phase 5 (T-16).</summary>
        [Obsolete("BoundProfileId is eliminated in Phase 5 (T-16). Use ActionId or ActionSlot.")]
        public string BoundProfileId => string.Empty;

        /// <summary>Reward configuration; committing or completing a card never grants it.</summary>
        public int MomentumReward { get; }

        /// <summary>Optional shared cooldown group; cards sharing a group share cooldown timers.</summary>
        public string SharedCooldownGroup { get; }

        public TacticalActionCardDefinition(string id, string name, string description, int baseMomentumCost,
            CharacterActionId actionId, float cooldownSeconds = 0f,
            TacticalTargetMode targetMode = TacticalTargetMode.Enemy, TacticalApproachMode approach = TacticalApproachMode.OptimalRange,
            int momentumReward = 0,
            CardDestination destinationAfterUse = CardDestination.Discard,
            string sharedCooldownGroup = null,
            TacticalActionSlot actionSlot = TacticalActionSlot.None)
            : base(id, name, description, baseMomentumCost, destinationAfterUse)
        {
            if (float.IsNaN(cooldownSeconds) || float.IsInfinity(cooldownSeconds) || cooldownSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(cooldownSeconds), "Cooldown must be finite and non-negative.");
            if (targetMode < TacticalTargetMode.Self || targetMode > TacticalTargetMode.None)
                throw new ArgumentOutOfRangeException(nameof(targetMode));
            if (approach != TacticalApproachMode.OptimalRange && approach != TacticalApproachMode.None)
                throw new ArgumentOutOfRangeException(nameof(approach));
            if (momentumReward < 0) throw new ArgumentOutOfRangeException(nameof(momentumReward));

            ActionId = actionId;
            ActionSlot = actionSlot;
            CooldownSeconds = cooldownSeconds;
            TargetMode = targetMode;
            Approach = approach;
            MomentumReward = momentumReward;
            SharedCooldownGroup = sharedCooldownGroup ?? string.Empty;

            if (actionId.IsValid && actionSlot != TacticalActionSlot.None)
            {
                ConfigurationWarning = "FixedActionOverridesSlot: ActionId " + actionId.Value + " overrides ActionSlot " + actionSlot;
            }
            else
            {
                ConfigurationWarning = string.Empty;
            }
        }

        public TacticalActionCardDefinition(string id, string name, string description, int baseMomentumCost,
            TacticalActionSlot actionSlot, float cooldownSeconds = 0f,
            TacticalTargetMode targetMode = TacticalTargetMode.Enemy, TacticalApproachMode approach = TacticalApproachMode.OptimalRange,
            int momentumReward = 0,
            CardDestination destinationAfterUse = CardDestination.Discard,
            string sharedCooldownGroup = null)
            : this(id, name, description, baseMomentumCost, default, cooldownSeconds, targetMode, approach, momentumReward, destinationAfterUse, sharedCooldownGroup, actionSlot)
        {
        }

        [Obsolete("boundProfileId is eliminated in Phase 5. Use constructor with ActionId or TacticalActionSlot instead.")]
        public TacticalActionCardDefinition(string id, string name, string description, int baseMomentumCost,
            CharacterActionId actionId, float cooldownSeconds,
            TacticalTargetMode targetMode, TacticalApproachMode approach,
            string boundProfileId, int momentumReward = 0,
            CardDestination destinationAfterUse = CardDestination.Discard,
            string sharedCooldownGroup = null)
            : this(id, name, description, baseMomentumCost, actionId, cooldownSeconds, targetMode, approach, momentumReward, destinationAfterUse, sharedCooldownGroup, TacticalActionSlot.None)
        {
        }

        [Obsolete("Use constructor taking CharacterActionId or TacticalActionSlot instead.")]
        public TacticalActionCardDefinition(string id, string name, string description, int baseMomentumCost,
            TacticalTargetMode targetMode, TacticalApproachMode approach,
            string boundProfileId, int momentumReward = 0,
            CardDestination destinationAfterUse = CardDestination.Discard)
            : this(id, name, description, baseMomentumCost, TacticalActionSlot.PrimaryAttack, 0f,
                targetMode, approach, momentumReward, destinationAfterUse)
        {
        }
    }

    /// <summary>Immediate utility data; no action, animation or physics ownership.</summary>
    public sealed class TacticalUtilityCardDefinition : TacticalCardDefinition
    {
        public override TacticalCardKind Kind => TacticalCardKind.Utility;
        public TacticalUtilityPayload Payload { get; }
        internal static TacticalUtilityCardDefinition CreatePublishedPreparation(CharacterTacticalCardReference reference,
            CharacterTacticalCardDefinition metadata)
        {
            if (reference == null || metadata == null || reference.CardId != metadata.CardId || reference.Revision != metadata.Revision
                || reference.ContentHash != metadata.ContentHash || metadata.InteractionPreparation == null
                || metadata.MotionCost != TacticalInteractionTrialCards.Cost || metadata.CooldownSeconds != 0f)
                throw new ArgumentException("Exact published fixed interaction contract required.");
            return new TacticalUtilityCardDefinition(reference.CardId, metadata.DisplayName, metadata.Description,
                metadata.MotionCost, metadata.InteractionPreparation, CardDestination.Exhaust) {
                PublishedCardId = reference.CardId, PublishedRevision = reference.Revision,
                PublishedContentHash = reference.ContentHash, DeckEntryInstanceId = reference.InstanceId, IconKey = metadata.IconKey };
        }

        public TacticalUtilityCardDefinition(string id, string name, string description, int baseMomentumCost,
            TacticalUtilityPayload payload, CardDestination destinationAfterUse = CardDestination.Discard)
            : base(id, name, description, baseMomentumCost, destinationAfterUse)
        {
            Payload = payload ?? throw new ArgumentNullException(nameof(payload));
        }
    }

    /// <summary>Immutable, explicitly typed effect data, without arbitrary execution callbacks.</summary>
    public abstract class TacticalUtilityPayload
    {
        public abstract UtilityPayloadKind Kind { get; }
    }

    /// <summary>Immediately grants momentum, capped by the ledger.</summary>
    public sealed class AddMomentumPayload : TacticalUtilityPayload
    {
        public override UtilityPayloadKind Kind => UtilityPayloadKind.AddMomentum;
        public int Amount { get; }
        public AddMomentumPayload(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            Amount = amount;
        }
    }

    /// <summary>
    /// Status binding for a future authority adapter. The standalone deck rejects this payload
    /// without spending or discarding, rather than pretending that a status was applied.
    /// </summary>
    public sealed class ApplyStatusPayload : TacticalUtilityPayload
    {
        public override UtilityPayloadKind Kind => UtilityPayloadKind.ApplyStatus;
        public string StatusDefinitionId { get; }
        public int StackCount { get; }
        public TacticalEffectLifetime Lifetime { get; }
        public int DurationTicks { get; }

        public ApplyStatusPayload(string statusDefinitionId, int stackCount,
            TacticalEffectLifetime lifetime, int durationTicks = 0)
        {
            if (string.IsNullOrWhiteSpace(statusDefinitionId)) throw new ArgumentException("A status ID is required.", nameof(statusDefinitionId));
            if (stackCount <= 0) throw new ArgumentOutOfRangeException(nameof(stackCount));
            if (lifetime < TacticalEffectLifetime.SimulationTicks || lifetime > TacticalEffectLifetime.Battle)
                throw new ArgumentOutOfRangeException(nameof(lifetime));
            if (durationTicks < 0 || (lifetime == TacticalEffectLifetime.SimulationTicks && durationTicks == 0))
                throw new ArgumentOutOfRangeException(nameof(durationTicks));
            StatusDefinitionId = statusDefinitionId;
            StackCount = stackCount;
            Lifetime = lifetime;
            DurationTicks = durationTicks;
        }
    }
}

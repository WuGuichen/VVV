using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>
    /// Single-threaded card and transaction owner. Construction/initialization allocate;
    /// play, cancellation, completion, indexed queries and shuffling reuse reserved storage.
    /// No world clock or executor is owned here. Call CommitTurn only after execution admission;
    /// call CompleteTurn only after the external coordinator confirms normal settle.
    /// </summary>
    public sealed partial class TacticalDeckManager
    {
        internal Func<TacticalCardDefinition, bool> DefinitionAvailability { get; set; }
        internal Func<TacticalCardDefinition, CardPlayEvaluation> DefinitionEvaluation { get; set; }
        internal Func<TacticalCardInstance, int, CardPlayEvaluation> InstanceEvaluation {get;set;}
        internal CardPlayEvaluation EvaluateAvailability(TacticalCardInstance card,int reservationCredit=0)
            => InstanceEvaluation!=null?InstanceEvaluation(card,reservationCredit):EvaluateDefinition(card.Definition);
        internal CardPlayEvaluation EvaluateDefinition(TacticalCardDefinition definition)
            => DefinitionEvaluation != null ? DefinitionEvaluation(definition)
                : new CardPlayEvaluation(DefinitionAvailability == null || DefinitionAvailability(definition)
                    ? CardPlayFailure.None : CardPlayFailure.EmptyActionSlot, 0, 0);
        internal bool IsDefinitionAvailable(TacticalCardDefinition definition) => EvaluateDefinition(definition).IsLegal;
        private List<TacticalCardInstance> _drawPile = new List<TacticalCardInstance>();
        private readonly List<TacticalCardInstance> _hand = new List<TacticalCardInstance>();
        private readonly List<TacticalCardInstance> _discardPile = new List<TacticalCardInstance>();
        private readonly List<TacticalCardInstance> _exhaustPile = new List<TacticalCardInstance>();
        private IMomentumLedger _pendingLedger;
        private ulong _reservationId;
        private IMomentumLedger _resolvingLedger;
        private ulong _resolvingReservationId;
        private ulong _nextInstanceId;
        private uint _randomState;
        private int _pendingHandIndex;
        private bool _initialized;
        private bool _replenishmentAvailable;

        public int HandCapacity { get; }
        public ReadOnlyCollection<TacticalCardInstance> DrawPile { get; private set; }
        public ReadOnlyCollection<TacticalCardInstance> Hand { get; }
        public ReadOnlyCollection<TacticalCardInstance> DiscardPile { get; }
        public ReadOnlyCollection<TacticalCardInstance> ExhaustPile { get; }
        public TacticalCardInstance PendingAction { get; private set; }
        internal TacticalExecutionInput PendingExecutionInput { get; private set; }
        public TacticalCardInstance ResolvingAction { get; private set; }
        public TacticalCooldownTracker CooldownTracker { get; private set; }
        public int CharacterId { get; set; }
        private Func<long> _tickProvider;

        public void BindCooldownTracker(TacticalCooldownTracker tracker, int characterId, Func<long> tickProvider = null)
        {
            CooldownTracker = tracker;
            CharacterId = characterId;
            _tickProvider = tickProvider;
        }

        public bool IsCardOnCooldown(TacticalCardInstance card, long currentTick = -1)
        {
            if (CooldownTracker == null || !(card?.Definition is TacticalActionCardDefinition actionCard)) return false;
            long tick = currentTick >= 0 ? currentTick : (_tickProvider != null ? _tickProvider() : 0);
            return CooldownTracker.IsOnCooldown(CharacterId, actionCard, tick);
        }

        public TacticalDeckManager(int handCapacity = 3, bool enableTestPreparation = false)
        {
            if (handCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(handCapacity));
            HandCapacity = handCapacity;
            SupportsTestPreparation = enableTestPreparation;
            DrawPile = _drawPile.AsReadOnly();
            Hand = _hand.AsReadOnly();
            DiscardPile = _discardPile.AsReadOnly();
            ExhaustPile = _exhaustPile.AsReadOnly();
        }

        /// <summary>
        /// Creates one instance per definition entry, shuffles deterministically and deals the opening hand.
        /// Duplicate definitions are allowed. Invalid input leaves the previous deck intact.
        /// Finish/cancel an outstanding action before reinitializing; no reservation is silently lost.
        /// </summary>
        public void InitializeDeck(IEnumerable<TacticalCardDefinition> definitions, int seed, int configuredInitialHandCount = 0)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            if (PendingAction != null || ResolvingAction != null)
                throw new InvalidOperationException("An action is still pending or resolving.");
            var cards = new List<TacticalCardInstance>();
            ulong nextId = _nextInstanceId;
            foreach (TacticalCardDefinition definition in definitions)
            {
                if (definition == null) throw new ArgumentException("Deck entries cannot be null.", nameof(definitions));
                if (nextId == ulong.MaxValue) throw new InvalidOperationException("Card identity space exhausted.");
                cards.Add(new TacticalCardInstance(++nextId, definition));
            }

            if (configuredInitialHandCount < 0 || configuredInitialHandCount > HandCapacity || configuredInitialHandCount > cards.Count)
                throw new ArgumentOutOfRangeException(nameof(configuredInitialHandCount));
            // Each destination may eventually hold the entire deck. Reserve before publishing it.
            _hand.Capacity = Math.Max(_hand.Capacity, Math.Min(HandCapacity, cards.Count));
            _discardPile.Capacity = Math.Max(_discardPile.Capacity, cards.Count);
            _exhaustPile.Capacity = Math.Max(_exhaustPile.Capacity, cards.Count);
            _drawPile = cards;
            DrawPile = cards.AsReadOnly();
            _hand.Clear();
            _discardPile.Clear();
            _exhaustPile.Clear();
            _nextInstanceId = nextId;
            PaidCardMomentum = 0;
            // The trial's explicitly configured opening hand is the definition prefix; only its reserve pile is shuffled.
            for (int i = 0; i < configuredInitialHandCount; i++)
            {
                cards[i].MoveTo(CardZone.Hand);
                _hand.Add(cards[i]);
            }
            if (configuredInitialHandCount > 0) cards.RemoveRange(0, configuredInitialHandCount);
            _randomState = unchecked((uint)seed);
            if (_randomState == 0) _randomState = 0x9E3779B9u;
            ShuffleDrawPile();
            _initialized = true;
            _replenishmentAvailable = true;
            ReplenishHand();
        }

        /// <summary>
        /// Consumes the opening/normal-turn replenishment window. Extra calls within a turn are no-ops,
        /// including after Utility use or selection. Retained cards keep identity, order and version.
        /// </summary>
        public void ReplenishHand()
        {
            if (!_replenishmentAvailable || PendingAction != null || ResolvingAction != null) return;
            _replenishmentAvailable = false;
            while (_hand.Count < HandCapacity)
            {
                if (_drawPile.Count == 0)
                {
                    if (_discardPile.Count == 0) break;
                    for (int i = 0; i < _discardPile.Count; i++)
                    {
                        TacticalCardInstance recycled = _discardPile[i];
                        recycled.MoveTo(CardZone.DrawPile);
                        _drawPile.Add(recycled);
                    }
                    _discardPile.Clear();
                    ShuffleDrawPile();
                }

                int last = _drawPile.Count - 1;
                TacticalCardInstance card = _drawPile[last];
                _drawPile.RemoveAt(last);
                card.MoveTo(CardZone.Hand);
                _hand.Add(card);
            }
        }

        /// <summary>Allocation-free local legality; target/profile admission belongs to the execution adapter.</summary>
        public CardPlayEvaluation EvaluateCard(TacticalCardInstance card, IMomentumLedger ledger, bool hasPendingAction = false, long currentTick = -1)
        {
            CanPlayCard(card, ledger, hasPendingAction, out CardPlayFailure failure, currentTick);
            var availability = card == null ? default : EvaluateAvailability(card);
            return new CardPlayEvaluation(failure, card == null ? 0 : Math.Max(card.Definition.BaseMomentumCost,availability.EffectiveCost),
                ledger == null ? 0 : ledger.Spendable, availability.BlockingActionId,
                availability.RemainingCooldownFrames, availability.SimulationTickRate);
        }

        public bool CanPlayCard(TacticalCardInstance card, IMomentumLedger ledger,
            bool hasPendingAction, out CardPlayFailure failure, long currentTick = -1)
        {
            if (!_initialized || ResolvingAction != null) failure = CardPlayFailure.WrongPhase;
            else if (card == null || card.Zone != CardZone.Hand || _hand.IndexOf(card) < 0)
                failure = CardPlayFailure.CardNotInHand;
            else if (ledger == null || (PendingAction != null && !ReferenceEquals(ledger, _pendingLedger)))
                failure = CardPlayFailure.InvalidLedger;
            else if (!EvaluateAvailability(card).IsLegal) failure = EvaluateAvailability(card).Failure;
            else if ((card.Definition.Kind == TacticalCardKind.Action || InteractionPreparationPayload.IsPreparation(card.Definition)) &&
                (hasPendingAction || PendingAction != null))
                failure = CardPlayFailure.ActionAlreadyPending;
            else if (card.Definition.Kind == TacticalCardKind.Action && !(card.Definition is TacticalActionCardDefinition))
                failure = CardPlayFailure.WrongCardKind;
            else if (card.Definition.Kind == TacticalCardKind.Utility &&
                (!(card.Definition is TacticalUtilityCardDefinition utility) ||
                    (!(utility.Payload is AddMomentumPayload) && !(SupportsTestPreparation && utility.Payload is TestPreparationPayload) && !(utility.Payload is InteractionPreparationPayload && (SupportsTestPreparation || card.Definition.HasPublishedBehavior)))))
                failure = CardPlayFailure.UnsupportedPayload;
            else if (card.Definition.Kind == TacticalCardKind.Action &&
                card.Definition is TacticalActionCardDefinition &&
                IsCardOnCooldown(card, currentTick))
                failure = CardPlayFailure.CardOnCooldown;
            else if (card.Definition.BaseMomentumCost > ledger.Spendable)
                failure = CardPlayFailure.InsufficientMomentum;
            else failure = CardPlayFailure.None;
            return failure == CardPlayFailure.None;
        }

        /// <summary>Selects one Action and reserves its frozen cost. Cancel first to switch cards.</summary>
        public CardPlayFailure SetPendingAction(TacticalCardInstance card, IMomentumLedger ledger, long currentTick = -1)
        {
            if (!CanPlayCard(card, ledger, false, out CardPlayFailure failure, currentTick)) return failure;
            if (!(card.Definition is TacticalActionCardDefinition) &&
                !(InteractionPreparationPayload.Matches(card.Definition) && (SupportsTestPreparation || card.Definition.HasPublishedBehavior) || SupportsTestPreparation && TestPreparationPayload.Matches(card.Definition))) return CardPlayFailure.WrongCardKind;
            if (!ledger.TryReserve(card.Definition.BaseMomentumCost, out ulong reservationId))
                return CardPlayFailure.ReservationInvalid;
            _pendingHandIndex = _hand.IndexOf(card);
            _hand.RemoveAt(_pendingHandIndex);
            PendingAction = card;
            _pendingLedger = ledger;
            _reservationId = reservationId;
            card.MoveTo(CardZone.PendingAction);
            return CardPlayFailure.None;
        }

        /// <summary>Releases only the owned reservation and returns the card to its retained hand position.</summary>
        public CardPlayFailure CancelPendingAction(IMomentumLedger ledger)
        {
            if (!_initialized || ResolvingAction != null) return CardPlayFailure.WrongPhase;
            if (PendingAction == null) return CardPlayFailure.NoPendingAction;
            if (!ReferenceEquals(ledger, _pendingLedger)) return CardPlayFailure.InvalidLedger;
            if (!ledger.ReleaseReservation(_reservationId)) return CardPlayFailure.ReservationInvalid;
            PendingAction.MoveTo(CardZone.Hand);
            _hand.Insert(Math.Min(_pendingHandIndex, _hand.Count), PendingAction);
            ClearPending();
            return CardPlayFailure.None;
        }

        /// <summary>Pays from spendable momentum, applies AddMomentum, then moves the card without drawing.</summary>
        public CardPlayFailure PlayUtilityCard(TacticalCardInstance card, IMomentumLedger ledger)
        {
            if (!CanPlayCard(card, ledger, false, out CardPlayFailure failure)) return failure;
            if (!(card.Definition is TacticalUtilityCardDefinition utility)) return CardPlayFailure.WrongCardKind;
            if (!(utility.Payload is AddMomentumPayload payload)) return CardPlayFailure.UnsupportedPayload;
            if (!ledger.TrySpend(card.Definition.BaseMomentumCost)) return CardPlayFailure.InsufficientMomentum;
            RecordCardPayment(card.Definition.BaseMomentumCost);
            ledger.Grant(payload.Amount);
            int index = _hand.IndexOf(card);
            if (PendingAction != null && index < _pendingHandIndex) _pendingHandIndex--;
            _hand.RemoveAt(index);
            MoveAfterUse(card);
            return CardPlayFailure.None;
        }

        /// <summary>
        /// Consumes the reservation exactly once and enters Resolving. Does not draw or grant hit rewards.
        /// Caller must have secured executor admission before invoking this local transaction.
        /// </summary>
        public CardPlayFailure CommitTurn(IMomentumLedger ledger, out TacticalCardInstance committedAction)
            => CommitTurn(ledger, false, out committedAction);

        internal CardPlayFailure CommitTurn(IMomentumLedger ledger, bool deferPayment, out TacticalCardInstance committedAction)
        {
            committedAction = null;
            if (!_initialized || ResolvingAction != null) return CardPlayFailure.WrongPhase;
            if (PendingAction == null) return CardPlayFailure.NoPendingAction;
            if (!ReferenceEquals(ledger, _pendingLedger)) return CardPlayFailure.InvalidLedger;
            if (!(PendingAction.Definition is TacticalActionCardDefinition)) return CardPlayFailure.WrongCardKind;
            if (!deferPayment && !ledger.CommitReservation(_reservationId)) return CardPlayFailure.ReservationInvalid;
            if (!deferPayment) RecordCardPayment(PendingAction.Definition.BaseMomentumCost);
            if (deferPayment) { _resolvingLedger = ledger; _resolvingReservationId = _reservationId; }
            committedAction = PendingAction;
            ResolvingAction = committedAction;
            committedAction.MoveTo(CardZone.Resolving);
            ClearPending();
            return CardPlayFailure.None;
        }

        /// <summary>NoAlloc. The original reservation survives admission until an accepted move or acknowledged pre-start termination.</summary>
        internal bool SettleResolvingReservation(IMomentumLedger ledger, bool started)
        {
            if (_resolvingReservationId == 0) return true;
            if (ResolvingAction == null || !ReferenceEquals(ledger, _resolvingLedger)) return false;
            bool settled = started ? ledger.CommitReservation(_resolvingReservationId) : ledger.ReleaseReservation(_resolvingReservationId);
            if (settled)
            {
                if (started) RecordCardPayment(ResolvingAction.Definition.BaseMomentumCost);
                _resolvingReservationId = 0; _resolvingLedger = null;
            }
            return settled;
        }

        /// <summary>
        /// External normal-settle receipt: completes only the matching resolving card, then refills.
        /// Duplicate or stale receipts cannot grant another turn. Never call for suspended/faulted execution.
        /// </summary>
        public bool CompleteTurn(ulong committedInstanceId, uint committedVersion)
        {
            if (_resolvingReservationId != 0) return false;
            if (ResolvingAction == null || ResolvingAction.InstanceId != committedInstanceId ||
                ResolvingAction.Version != committedVersion) return false;
            MoveAfterUse(ResolvingAction);
            ResolvingAction = null;
            _replenishmentAvailable = true;
            ReplenishHand();
            return true;
        }

        internal void ClearPendingExecutionInput() => PendingExecutionInput = default;

        internal TacticalInputStatus SetPendingExecutionInput(in TacticalExecutionInput input,
            in TacticalExecutionInputBinding expected)
        {
            if (PendingAction == null) return TacticalInputStatus.MissingInput;
            if (!expected.IsValid || expected.CardInstance != PendingAction.InstanceId || expected.CardVersion != PendingAction.Version
                || !input.Binding.Equals(expected)) return TacticalInputStatus.StaleBinding;
            if (input.Kind == TacticalExecutionInputKind.None) return TacticalInputStatus.MissingInput;
            PendingExecutionInput = input;
            return TacticalInputStatus.Valid;
        }

        private void ClearPending()
        {
            PendingAction = null;
            PendingExecutionInput = default;
            _pendingLedger = null;
            _reservationId = 0;
        }

        private void MoveAfterUse(TacticalCardInstance card)
        {
            bool exhaust = card.Definition.DestinationAfterUse == CardDestination.Exhaust;
            card.MoveTo(exhaust ? CardZone.ExhaustPile : CardZone.DiscardPile);
            (exhaust ? _exhaustPile : _discardPile).Add(card);
        }

        private void ShuffleDrawPile()
        {
            for (int i = _drawPile.Count - 1; i > 0; i--)
            {
                // Explicit xorshift32 state avoids global Unity randomness and runtime-specific System.Random.
                _randomState ^= _randomState << 13;
                _randomState ^= _randomState >> 17;
                _randomState ^= _randomState << 5;
                int j = (int)(_randomState % (uint)(i + 1));
                TacticalCardInstance temporary = _drawPile[i];
                _drawPile[i] = _drawPile[j];
                _drawPile[j] = temporary;
            }
        }
    }
}

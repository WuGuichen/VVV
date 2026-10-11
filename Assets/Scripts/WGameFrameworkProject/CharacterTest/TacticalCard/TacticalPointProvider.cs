using System;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    internal enum TacticalPointProviderCapability { Unsupported = 0, WorldPoint = 1 }

    /// <summary>Owner-captured request; the provider selects a point, never changes card rules or identity.</summary>
    internal readonly struct TacticalPointProviderRequest
    {
        public readonly TacticalExecutionInputBinding Binding;
        public readonly TacticalWorldPoint Origin;
        public readonly TacticalPointInputRules Rules;
        public TacticalPointProviderRequest(TacticalExecutionInputBinding binding, TacticalWorldPoint origin, TacticalPointInputRules rules)
        { Binding = binding; Origin = origin; Rules = rules; }
    }

    /// <summary>Reusable automatic point input contract. Unsupported/unavailable never falls back to a random or default point.</summary>
    internal interface ITacticalPointProvider
    {
        TacticalPointProviderCapability Capability { get; }
        TacticalInputStatus TryGetPoint(in TacticalPointProviderRequest request, out TacticalWorldPoint point);
    }

    internal static class TacticalPointProviderInput
    {
        internal static TacticalInputStatus TryCreate(ITacticalPointProvider provider, in TacticalPointProviderRequest request,
            out TacticalExecutionInput input)
        {
            input = default;
            if (!request.Binding.IsValid || !request.Rules.IsConfigured) return TacticalInputStatus.InvalidIdentity;
            if (provider == null || provider.Capability != TacticalPointProviderCapability.WorldPoint)
                return TacticalInputStatus.UnsupportedProvider;
            var status = provider.TryGetPoint(request, out var point);
            if (status == TacticalInputStatus.UnsupportedProvider || status == TacticalInputStatus.PointUnavailable) return status;
            if (status != TacticalInputStatus.Valid) return TacticalInputStatus.PointUnavailable;
            input = new TacticalExecutionInput(request.Binding, TacticalExecutionInputSource.Automatic, point, request.Origin);
            return TacticalInputStatus.Valid;
        }
    }
    /// <summary>Borrowed execution owner; selection never starts a second Overlay evaluator or ledger.</summary>
    internal interface ITacticalAutomaticCardExecutionPort
    {
        bool CanBind(MxFramework.Gameplay.GameplayEntityId actor, TacticalActionCardDefinition card, out string issue);
        bool TryBegin(MxFramework.Gameplay.GameplayEntityId actor, TacticalActionCardDefinition card,
            long frame, out ulong executionId, out string issue);
        ExecutionObservation Observe(MxFramework.Gameplay.GameplayEntityId actor, ulong executionId);
    }

    /// <summary>Uses a caller-owned Stage/Deck/Ledger/Driver and its original command consumer.</summary>
    internal sealed class TacticalStageAutomaticCardExecutionPort : ITacticalAutomaticCardExecutionPort
    {
        private readonly TacticalStageFlowCoordinator _stage;
        private readonly TacticalCommandPump _commands;
        private readonly int _source;
        private ulong _sequence, _executionId;
        private MxFramework.Gameplay.GameplayEntityId _actor;
        internal TacticalStageAutomaticCardExecutionPort(TacticalStageFlowCoordinator stage, TacticalCommandPump commands, int source = 4)
        {
            _stage = stage ?? throw new ArgumentNullException(nameof(stage));
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
            if (!commands.Uses(stage)) throw new ArgumentException("Automatic execution must use the Stage's original command consumer.");
            if (!commands.HasSource(source)) throw new ArgumentOutOfRangeException(nameof(source));
            _source = source;
        }
        public bool CanBind(MxFramework.Gameplay.GameplayEntityId actor, TacticalActionCardDefinition card, out string issue)
        {
            issue = "execution-input:UnsupportedProvider";
            try { if (!_stage.HasAutomaticPointProvider) return false; }
            catch (Exception) { issue = "execution-input:PointUnavailable"; return false; }
            issue = "execution:AutomaticOwnerBindingMismatch";
            if (_stage.IsReleased || !_stage.TryGetRoster(out var owner, out _) || owner != actor || card?.PointMove == null) return false;
            bool found = Contains(_stage.Deck.Hand, card) || Contains(_stage.Deck.DrawPile, card)
                || Contains(_stage.Deck.DiscardPile, card) || Contains(_stage.Deck.ExhaustPile, card)
                || ReferenceEquals(_stage.Deck.PendingAction?.Definition, card)
                || ReferenceEquals(_stage.Deck.ResolvingAction?.Definition, card);
            if (!found) return false;
            issue = string.Empty; return true;
        }
        private static bool Contains(System.Collections.Generic.IReadOnlyList<TacticalCardInstance> cards, TacticalActionCardDefinition definition)
        {
            for (int i = 0; i < cards.Count; i++) if (ReferenceEquals(cards[i].Definition, definition)) return true;
            return false;
        }
        public bool TryBegin(MxFramework.Gameplay.GameplayEntityId actor, TacticalActionCardDefinition definition,
            long frame, out ulong executionId, out string issue)
        {
            executionId = 0;
            if (!CanBind(actor, definition, out issue)) return false;
            issue = "execution:AutomaticIngressBusy";
            if (_stage.Phase != TacticalStagePhase.Planning || _stage.Health != TacticalStageHealth.Running
                || _stage.Deck.PendingAction != null || _stage.Deck.ResolvingAction != null
                || _commands.PendingCount != 0 || _commands.ReceiptCount != 0) return false;
            issue = "execution:StaleFrame";
            if (_stage.LastCompletedFrame == long.MaxValue || frame != _stage.LastCompletedFrame + 1) return false;
            TacticalCardInstance selected = null;
            for (int i = 0; i < _stage.Deck.Hand.Count; i++)
                if (ReferenceEquals(_stage.Deck.Hand[i].Definition, definition)) { selected = _stage.Deck.Hand[i]; break; }
            issue = "execution:CardNotInHand";
            if (selected == null) return false;
            if (!_stage.Deck.CanPlayCard(selected, _stage.Ledger, false, out var selectionFailure))
            { issue = "execution:" + selectionFailure; return false; }
            if (!Send(selected, TacticalStageCommandKind.SelectAction, out issue)) return false;
            if (!Send(selected, TacticalStageCommandKind.ConfirmAutomaticExecution, out issue))
            {
                // Failed automatic admission has no interactive pending owner: undo only our selection.
                string admissionIssue = issue;
                if (ReferenceEquals(_stage.Deck.PendingAction, selected)
                    && !Send(selected, TacticalStageCommandKind.CancelPending, out var rollbackIssue))
                    issue = admissionIssue + "; rollback:" + rollbackIssue;
                else issue = admissionIssue;
                return false;
            }
            _actor = actor; _executionId = executionId = _stage.Driver.ExecutionId;
            issue = string.Empty; return executionId != 0;
        }
        private bool Send(TacticalCardInstance card, TacticalStageCommandKind kind, out string issue)
        {
            issue = "execution:AutomaticIngressBusy";
            if (_commands.PendingCount != 0 || _commands.ReceiptCount != 0) return false;
            ulong previous = _commands.GetLastAcceptedSequence(_source);
            if (_sequence < previous) _sequence = previous;
            if (_sequence == ulong.MaxValue) { issue = "execution:IdentityExhausted"; return false; }
            var command = new TacticalStageCommand(_stage.StageRunId, _stage.TurnIndex, card.InstanceId,
                card.Version, _stage.SnapshotRevision, kind);
            var queued = _commands.TrySubmit(command, _source, ++_sequence);
            if (queued != TacticalStageRejection.None) { issue = "execution:" + queued; return false; }
            if (_commands.Drain(1) != 1 || !_commands.TryReadReceipt(out var receipt)) return false;
            if (receipt.Rejection != TacticalStageRejection.None)
            {
                issue = kind == TacticalStageCommandKind.ConfirmAutomaticExecution && !string.IsNullOrEmpty(_stage.ExecutionIssue)
                    ? _stage.ExecutionIssue : "execution:" + receipt.Rejection;
                return false;
            }
            issue = string.Empty; return true;
        }
        public ExecutionObservation Observe(MxFramework.Gameplay.GameplayEntityId actor, ulong executionId)
        {
            if (executionId == 0 || executionId != _executionId || actor != _actor
                || (!_stage.IsReleased && (!_stage.TryGetRoster(out var owner, out _) || owner != actor))) return default;
            var value = _stage.Driver.ObserveOwnedExecution(executionId);
            return new ExecutionObservation(value.ExecutionId, value.ActionStarted, value.ActionTimelineCompleted,
                value.BehaviorTerminal && (_stage.Driver.ExecutionId == 0 || _stage.IsReleased),
                value.OwnedIntentsReleased, value.CommandsAcknowledged, value.ExecutionStarted,
                string.IsNullOrEmpty(value.Issue) ? _stage.ExecutionIssue : value.Issue, value.ExecutionStartedTick);
        }
    }

    /// <summary>Exact deployment binding for a deferred point source. No point is stored here.</summary>
    internal sealed class TacticalAutomaticExecutionBinding
    {
        internal TacticalAutomaticExecutionBinding(TacticalActionCardDefinition card, ITacticalAutomaticCardExecutionPort owner)
        { Card = card ?? throw new ArgumentNullException(nameof(card)); Owner = owner ?? throw new ArgumentNullException(nameof(owner)); }
        internal TacticalActionCardDefinition Card { get; }
        internal ITacticalAutomaticCardExecutionPort Owner { get; }
        internal bool Matches(CharacterTestTacticalFrozenSource frozen)
        {
            var source = frozen?.CloneLegacySource()?.ExecutionInput;
            var rules = Card.PointMove;
            return source != null && rules != null && frozen.WorkspaceId == "published:" + Card.PublishedCardId
                && frozen.CardRevision == Card.PublishedRevision && frozen.CharacterId > 0
                && source.RangeMillimeters == rules.Input.RadiusMillimeters && source.ArrivalMillimeters == rules.ArrivalMillimeters
                && source.MaximumFrames == rules.MaximumFrames && source.SpeedMilli == rules.SpeedMilli
                && source.RequireReachable == rules.Input.RequireReachable;
        }
        internal bool TryBegin(MxFramework.Gameplay.GameplayEntityId actor, long frame, out ulong executionId, out string issue)
            => Owner.TryBegin(actor, Card, frame, out executionId, out issue);
    }

}

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>Whether a card occupies the turn's sole action slot.</summary>
    public enum TacticalCardKind { Action, Utility }

    /// <summary>Exclusive location of a battle card instance.</summary>
    public enum CardZone { DrawPile, Hand, PendingAction, Resolving, DiscardPile, ExhaustPile }

    /// <summary>Permanent destination after successful use or action completion.</summary>
    public enum CardDestination { Discard, Exhaust }

    /// <summary>Target data required by the future execution adapter.</summary>
    public enum TacticalTargetMode { Self, Enemy, Direction, None }

    /// <summary>Approach intent; None does not bypass the action gateway.</summary>
    public enum TacticalApproachMode { OptimalRange, None }

    /// <summary>Expected rejection reasons; failed operations do not consume cards.</summary>
    public enum CardPlayFailure
    {
        None, WrongPhase, CardNotInHand, InsufficientMomentum, ActionAlreadyPending,
        WrongCardKind, NoPendingAction, InvalidLedger, ReservationInvalid, UnsupportedPayload,
        CardOnCooldown, MissingActionOrSlotBinding, EmptyActionSlot, BoundActionOnCooldown, BoundActionUnavailable, CharacterUnavailable, WeaponTypeMismatch, WeaponTypeUnclassified
    }

    /// <summary>
    /// Tactical action slot container abstraction for generalized battle cards.
    /// Dynamically resolved to physical CharacterActionId from character equipment/loadout.
    /// </summary>
    public enum TacticalActionSlot
    {
        None = 0,
        PrimaryAttack = 1,
        SecondaryAttack = 2,
        Skill = 3,
        Mobility = 4,
        Guard = 5,
    }

    /// <summary>Explicit payload discriminator, independent of CLR type names.</summary>
    public enum UtilityPayloadKind { AddMomentum, ApplyStatus, TestPreparation, InteractionPreparation }

    /// <summary>Lifetime semantics for an external status authority.</summary>
    public enum TacticalEffectLifetime { SimulationTicks, NextActionStart, UntilExecutionTerminal, Battle }
}

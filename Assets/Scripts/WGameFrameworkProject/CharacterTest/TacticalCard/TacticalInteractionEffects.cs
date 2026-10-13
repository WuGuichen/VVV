using System;
using System.Collections.Generic;
using MxFramework.Combat.Hit;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    internal static class TacticalInteractionHashFields
    {
        internal const int Stage = 325;
        internal const int Turn = 326;
        internal const int ActorIndex = 327;
        internal const int ActorGeneration = 328;
        internal const int NextActivation = 329;
        internal const int UsedMask = 330;
        internal const int LastReceiptFrame = 331;
        internal const int ReturnPending = 332;
        internal const int ReturnResult = 333;
        internal const int SlotIndex = 334;
        internal const int SlotActive = 335;
        internal const int SlotActivation = 336;
        internal const int ArmedFrame = 337;
        internal const int ExpiresFrame = 338;
        internal const int TargetIndex = 339;
        internal const int TargetGeneration = 340;
        internal const int X = 341;
        internal const int Y = 342;
        internal const int Z = 343;
        internal const int ReceiptCount = 344;
        internal const int ReceiptFrame = 345;
        internal const int AttackerIndex = 346;
        internal const int AttackerGeneration = 347;
        internal const int ReceiptTargetIndex = 348;
        internal const int ReceiptTargetGeneration = 349;
        internal const int ActionInstance = 350;
        internal const int Trace = 351;
        internal const int CardInstance = 352;
        internal const int SourceVersion = 353;
        internal const int ReceiptStage = 354;
        internal const int ReceiptTurn = 355;
        internal const int Accessories = 356;
        internal const int ProducerKind = 357, ProducerId = 358, ProducerGeneration = 359, ProducerOccurrence = 360;
    }

    // Candidate discriminators only. Publication/definition ID allocation belongs to the repository owner.
    internal enum TacticalInteractionKind { PursuitMark = 1, MomentumRecovery = 2, DefensePreparation = 3, ReturnMark = 4, HurtCharge = 5 }
    internal enum TacticalInteractionFailure { None, Unbound, AlreadyUsedThisTurn, InvalidTarget, InvalidPlacement, DefenseResourceUnavailable, InvalidFrame }
    internal enum TacticalReturnResult { None, WaitingForRelease, Returned, DestinationBlocked, Expired }

    public sealed class InteractionPreparationPayload : TacticalUtilityPayload
    {
        public override UtilityPayloadKind Kind => UtilityPayloadKind.InteractionPreparation;
        internal TacticalInteractionKind Effect { get; }
        internal InteractionPreparationPayload(TacticalInteractionKind effect) { Effect = effect; }
        internal static bool Matches(TacticalCardDefinition definition) => definition is TacticalUtilityCardDefinition card && card.Payload is InteractionPreparationPayload;
        internal static bool IsPreparation(TacticalCardDefinition definition) => Matches(definition) || TestPreparationPayload.Matches(definition);
    }

    internal static class TacticalInteractionTrialCards
    {
        internal const int Cost = 5, FixedDamage = 8, MomentumGain = 3, ScatterPressure = 12, DurationFrames = 600;
        internal static readonly string[] Ids = { "lab.pursuit-mark", "lab.momentum-recovery", "lab.defense-preparation", "lab.return-mark", "lab.hurt-charge" };
        private static readonly string[] Names = { "追击印记", "动能回收", "破防准备", "换位标记", "受击蓄能" };
        private static readonly string[] Text = {
            "标记当前敌人。本回合下一次对其有效命中，追加8点基础伤害一次（仍经现有护甲结算）。护甲吸收不使命中失效。",
            "本回合下一次有效命中获得3动能一次；护甲吸收仍算命中。",
            "本回合下一次主动攻击被普通防御挡住时，额外施加12点散势压力一次；不触发命中类效果。",
            "记录当前落地点。本回合下一次有效命中后挂待回返，等待动作释放、落地与外力稳定，在首个安全运动边界尝试回返一次；净空失败也清记录。不承诺瞬时返回。",
            "本回合首次受到外来实际HP伤害获得3动能一次。" };
        internal static string Name(TacticalInteractionKind kind) => Names[(int)kind - 1];
        internal static string Description(TacticalInteractionKind kind) => Text[(int)kind - 1];
        internal static bool TryCreate(string id, out TacticalUtilityCardDefinition card)
        {
            int index = Array.IndexOf(Ids, id); card = null;
            if (index < 0) return false;
            card = new TacticalUtilityCardDefinition(id, Names[index] + "（试用）", Text[index] + " 费用5；最多600模拟帧，同类每回合一次；不占行动。",
                Cost, new InteractionPreparationPayload((TacticalInteractionKind)(index + 1)), CardDestination.Exhaust);
            return true;
        }
    }

    internal readonly struct TacticalInteractionReceipt : IEquatable<TacticalInteractionReceipt>
    {
        internal readonly ulong Stage, Turn;
        internal readonly long Frame;
        internal readonly GameplayEntityId Attacker, Target;
        internal readonly int ActionInstance, Trace;
        internal readonly CombatHitProducerIdentity ProducerIdentity;
        internal TacticalInteractionReceipt(ulong stage, ulong turn, long frame, GameplayEntityId attacker, GameplayEntityId target, int actionInstance, int trace, CombatHitProducerIdentity producerIdentity = default)
        { ProducerIdentity = producerIdentity; Stage = stage; Turn = turn; Frame = frame; Attacker = attacker; Target = target; ActionInstance = actionInstance; Trace = trace; }
        public override int GetHashCode() => unchecked((int)(Stage ^ (Stage >> 32) ^ Turn ^ (Turn >> 32) ^ (ulong)Frame) * 397 ^ Attacker.GetHashCode() * 31 ^ Target.GetHashCode() * 17 ^ ActionInstance ^ Trace ^ ProducerIdentity.GetHashCode());
        public bool Equals(TacticalInteractionReceipt other) => Stage == other.Stage && Turn == other.Turn && Frame == other.Frame && Attacker == other.Attacker && Target == other.Target && ActionInstance == other.ActionInstance && Trace == other.Trace && ProducerIdentity.Equals(other.ProducerIdentity);
    }

    internal struct TacticalInteractionSlot
    {
        public bool Active;
        public ulong Activation, CardInstance;
        public uint SourceVersion;
        public long ArmedFrame, ExpiresFrame;
        public GameplayEntityId Target;
        public int X, Y, Z;
    }

    internal sealed class TacticalInteractionSnapshot
    {
        public const int CurrentVersion = 2;
        public int Version { get; set; } = CurrentVersion;
        public ulong Stage { get; set; }
        public ulong Turn { get; set; }
        public GameplayEntityId Actor { get; set; }
        public ulong NextActivation { get; set; }
        public int UsedMask { get; set; }
        public long LastReceiptFrame { get; set; } = -1;
        public TacticalInteractionSlot[] Slots { get; set; } = new TacticalInteractionSlot[5];
        public TacticalInteractionReceiptData[] Receipts { get; set; } = Array.Empty<TacticalInteractionReceiptData>();
        public bool ReturnPending { get; set; }
        public TacticalReturnResult ReturnResult { get; set; }
        [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public TacticalAccessoryAuthoritySnapshot Accessories { get; set; }
        public TacticalInteractionSnapshot Clone() => new TacticalInteractionSnapshot {
            Version = Version, Stage = Stage, Turn = Turn, Actor = Actor, NextActivation = NextActivation,
            UsedMask = UsedMask, LastReceiptFrame = LastReceiptFrame, Slots = (TacticalInteractionSlot[])Slots.Clone(),
            Receipts = (TacticalInteractionReceiptData[])Receipts.Clone(), ReturnPending = ReturnPending, ReturnResult = ReturnResult, Accessories = Accessories?.Clone() };
        internal bool IsValid(long frame = long.MaxValue)
        {
            if (Version != CurrentVersion || Slots == null || Slots.Length != 5 || Receipts == null || Receipts.Length > TacticalInteractionEffectAuthority.MaxReceipts
                || UsedMask < 0 || UsedMask > 31 || LastReceiptFrame < -1 || LastReceiptFrame > frame
                || ReturnResult < TacticalReturnResult.None || ReturnResult > TacticalReturnResult.Expired) return false;
            if (Stage == 0 && (NextActivation != 0 || LastReceiptFrame != -1 || ReturnResult != TacticalReturnResult.None || Actor.IsValid || Turn != 0 || UsedMask != 0 || ReturnPending || Receipts.Length != 0)) return false;
            if (Stage != 0 && (!Actor.IsValid || Turn == 0)) return false;
            if (Accessories != null && !Accessories.IsValid(Stage, Turn, Actor, frame)) return false;
            ulong previous = 0; int activations = 0; var activationIds = new HashSet<ulong>();
            for (int i = 0; i < Slots.Length; i++)
            {
                var slot = Slots[i];
                if (slot.Activation == 0) { if ((UsedMask & (1 << i)) != 0 || slot.Active || slot.ArmedFrame != 0 || slot.ExpiresFrame != 0 || slot.Target.IsValid || slot.CardInstance != 0 || slot.SourceVersion != 0 || slot.X != 0 || slot.Y != 0 || slot.Z != 0) return false; continue; }
                if (!activationIds.Add(slot.Activation)) return false; activations++;
                if (slot.CardInstance == 0 || slot.SourceVersion == 0 || slot.Activation > NextActivation || slot.ArmedFrame < 0 || slot.ArmedFrame > frame
                    || slot.ArmedFrame > long.MaxValue - TacticalInteractionTrialCards.DurationFrames
                    || slot.ExpiresFrame != slot.ArmedFrame + TacticalInteractionTrialCards.DurationFrames
                    || (UsedMask & (1 << i)) == 0 || (i == 0 && (!slot.Target.IsValid || slot.Target == Actor))) return false;
                previous = Math.Max(previous, slot.Activation);
            }
            var returned = Slots[3];
            bool returnTerminal = ReturnResult == TacticalReturnResult.Returned || ReturnResult == TacticalReturnResult.DestinationBlocked || ReturnResult == TacticalReturnResult.Expired;
            if (returned.Activation == 0 && (ReturnPending || ReturnResult != TacticalReturnResult.None)) return false;
            if (returned.Active && (ReturnPending || ReturnResult != TacticalReturnResult.None)) return false;
            if (returnTerminal && (returned.Activation == 0 || returned.Active || ReturnPending || returned.X != 0 || returned.Y != 0 || returned.Z != 0)) return false;
            if (NextActivation != (ulong)activations || NextActivation < previous
                || (ReturnResult == TacticalReturnResult.WaitingForRelease) != ReturnPending
                || (ReturnPending && (Slots[3].Activation == 0 || Slots[3].Active || ReturnResult != TacticalReturnResult.WaitingForRelease))) return false;
            var unique = new HashSet<TacticalInteractionReceipt>();
            for (int i = 0; i < Receipts.Length; i++)
            {
                var receipt = Receipts[i];
                if (!receipt.HasValidProducer || receipt.Stage != Stage || receipt.Turn != Turn || receipt.Frame < 0 || receipt.Frame > LastReceiptFrame || receipt.Frame > frame || !receipt.Attacker.IsValid || !receipt.Target.IsValid) return false;
                if (!unique.Add(receipt.ToReceipt()) || (i > 0 && Receipts[i - 1].Frame > receipt.Frame)) return false;
            }
            if (Receipts.Length == 0 ? LastReceiptFrame != -1 : Receipts[Receipts.Length - 1].Frame != LastReceiptFrame) return false;
            return true;
        }
    }

    internal struct TacticalInteractionReceiptData
    {
        public ulong Stage, Turn;
        public long Frame;
        public GameplayEntityId Attacker, Target;
        public int ActionInstance, Trace;
        public int ProducerKind;
        public ulong ProducerId, ProducerGeneration, ProducerOccurrence;
        internal bool HasValidProducer => ProducerKind == 0
            ? ProducerId == 0 && ProducerGeneration == 0 && ProducerOccurrence == 0
            : ProducerKind >= 1 && ProducerKind <= (int)CombatHitProducerKind.DerivedDamage && ProducerId != 0;
        internal CombatHitProducerIdentity ProducerIdentity => ProducerKind == 0 ? default
            : new CombatHitProducerIdentity((CombatHitProducerKind)ProducerKind, ProducerId, ProducerGeneration, ProducerOccurrence);
        internal void AddProducerHash(ref RuntimeHashAccumulator hash, int kindField, int idField, int generationField, int occurrenceField)
        {
            if (ProducerKind == 0) return; // Preserve hashes for existing non-spatial receipts.
            hash.AddInt(kindField, ProducerKind); hash.AddLong(idField, unchecked((long)ProducerId));
            hash.AddLong(generationField, unchecked((long)ProducerGeneration)); hash.AddLong(occurrenceField, unchecked((long)ProducerOccurrence));
        }
        internal TacticalInteractionReceiptData(in TacticalInteractionReceipt value)
        { ProducerKind = (int)value.ProducerIdentity.Kind; ProducerId = value.ProducerIdentity.Id;
            ProducerGeneration = value.ProducerIdentity.Generation; ProducerOccurrence = value.ProducerIdentity.Occurrence; Stage = value.Stage; Turn = value.Turn; Frame = value.Frame; Attacker = value.Attacker; Target = value.Target; ActionInstance = value.ActionInstance; Trace = value.Trace; }
        internal TacticalInteractionReceipt ToReceipt() => new TacticalInteractionReceipt(Stage, Turn, Frame, Attacker, Target, ActionInstance, Trace, ProducerIdentity);
    }

    /// <summary>One actor generation's effect authority. No clock, action, card or resource copy is owned here.</summary>
    internal sealed partial class TacticalInteractionEffectAuthority
    {
        internal const int MaxReceipts = 8192;
        private TacticalInteractionSnapshot _state = new TacticalInteractionSnapshot();
        private readonly HashSet<TacticalInteractionReceipt> _seen = new HashSet<TacticalInteractionReceipt>();
        private readonly List<TacticalInteractionReceiptData> _receipts = new List<TacticalInteractionReceiptData>(128);
        internal ulong Stage => _state.Stage;
        internal ulong Turn => _state.Turn;
        internal int UsedMask => _state.UsedMask;
        internal int ActiveMask { get { int value = 0; for (int i = 0; i < 5; i++) if (_state.Slots[i].Active) value |= 1 << i; return value; } }
        internal bool ReturnPending => _state.ReturnPending;
        internal TacticalReturnResult ReturnResult => _state.ReturnResult;
        internal GameplayEntityId Actor => _state.Actor;

        internal void Bind(ulong stage, ulong turn, GameplayEntityId actor)
        {
            if (stage == 0 || turn == 0 || !actor.IsValid) throw new ArgumentException("A current stage/turn/actor is required.");
            if (_state.Stage == stage && _state.Turn == turn && _state.Actor == actor) return;
            bool preserveAccessories = _state.Stage == stage && _state.Actor == actor && turn > _state.Turn;
            if (preserveAccessories) { _state = new TacticalInteractionSnapshot(); _receipts.Clear(); _seen.Clear(); }
            else Clear();
            _state.Stage = stage; _state.Turn = turn; _state.Actor = actor;
            if (preserveAccessories) AdvanceAccessories(turn);
        }
        internal void Clear() { _state = new TacticalInteractionSnapshot(); _receipts.Clear(); _seen.Clear(); ClearAccessories(); ConfirmedFixedAssembly = null; }
        internal void AdvanceTurn(ulong stage, ulong turn, GameplayEntityId actor) => Bind(stage, turn, actor);
        internal void Expire(long frame)
        {
            for (int i = 0; i < 5; i++) if (_state.Slots[i].Activation != 0 && frame > _state.Slots[i].ExpiresFrame) _state.Slots[i].Active = false;
            if (_state.ReturnPending && frame > _state.Slots[3].ExpiresFrame) { _state.ReturnPending = false; _state.ReturnResult = TacticalReturnResult.Expired; }
            if (_state.Slots[3].Activation != 0 && frame > _state.Slots[3].ExpiresFrame)
            { var slot = _state.Slots[3]; slot.X = slot.Y = slot.Z = 0; _state.Slots[3] = slot;
                if (_state.ReturnResult == TacticalReturnResult.None) _state.ReturnResult = TacticalReturnResult.Expired; }
        }
        internal TacticalInteractionFailure Validate(TacticalInteractionKind kind, long frame, GameplayEntityId target, bool placementValid, bool defenseAvailable)
        {
            if (_state.Stage == 0 || !_state.Actor.IsValid) return TacticalInteractionFailure.Unbound;
            if (frame < 0 || frame > long.MaxValue - TacticalInteractionTrialCards.DurationFrames || _state.NextActivation == ulong.MaxValue) return TacticalInteractionFailure.InvalidFrame;
            int index = (int)kind - 1;
            if (index < 0 || index >= 5) return TacticalInteractionFailure.Unbound;
            if ((_state.UsedMask & (1 << index)) != 0) return TacticalInteractionFailure.AlreadyUsedThisTurn;
            if (kind == TacticalInteractionKind.PursuitMark && (!target.IsValid || target == _state.Actor)) return TacticalInteractionFailure.InvalidTarget;
            if (kind == TacticalInteractionKind.ReturnMark && !placementValid) return TacticalInteractionFailure.InvalidPlacement;
            if (kind == TacticalInteractionKind.DefensePreparation && !defenseAvailable) return TacticalInteractionFailure.DefenseResourceUnavailable;
            return TacticalInteractionFailure.None;
        }
        // Only call after successful deck payment; no external callback, allocation or rejection occurs here.
        internal void ActivateValidated(TacticalInteractionKind kind, long frame, GameplayEntityId target, int x, int y, int z, ulong cardInstance, uint sourceVersion)
        {
            int index = (int)kind - 1;
            _state.Slots[index] = new TacticalInteractionSlot { Active = true, Activation = ++_state.NextActivation,
                CardInstance = cardInstance, SourceVersion = sourceVersion, ArmedFrame = frame, ExpiresFrame = frame + TacticalInteractionTrialCards.DurationFrames, Target = target, X = x, Y = y, Z = z };
            _state.UsedMask |= 1 << index;
        }
        private bool Eligible(int index, long frame) => _state.Slots[index].Active && frame > _state.Slots[index].ArmedFrame && frame <= _state.Slots[index].ExpiresFrame;
        /// <summary>Called only after actual HP/defense commit. Returns a derived base-damage request, never recursively processes it.</summary>
        /// <remarks>NoAlloc after receipt capacity warms; bounded receipt storage fails closed if exhausted.</remarks>
        internal int CommitReceipt(in TacticalInteractionReceipt receipt, int actualHpLoss, bool validActiveHit, bool ordinaryBlocked,
            bool derived, IMomentumLedger ledger, bool defensePressureAvailable = false)
            => CommitReceipt(receipt, actualHpLoss, validActiveHit, ordinaryBlocked, derived, ledger, out _, defensePressureAvailable);
        internal int CommitReceipt(in TacticalInteractionReceipt receipt, int actualHpLoss, bool validActiveHit, bool ordinaryBlocked,
            bool derived, IMomentumLedger ledger, out int scatterPressure, bool defensePressureAvailable = false)
        {
            scatterPressure = 0;
            if (_state.Stage == 0 || receipt.Stage != _state.Stage || receipt.Turn != _state.Turn || derived || !receipt.Attacker.IsValid || !receipt.Target.IsValid || receipt.Frame < _state.LastReceiptFrame) return 0;
            if (receipt.Attacker != _state.Actor && receipt.Target != _state.Actor) return 0;
            if (_seen.Contains(receipt)) return 0;
            if (_receipts.Count == MaxReceipts) return 0;
            _seen.Add(receipt); _receipts.Add(new TacticalInteractionReceiptData(receipt)); _state.LastReceiptFrame = receipt.Frame;
            Expire(receipt.Frame);
            int extraDamage = 0;
            bool outgoing = receipt.Attacker == _state.Actor && receipt.Target != _state.Actor && validActiveHit;
            if (outgoing)
            {
                if (Eligible(0, receipt.Frame) && _state.Slots[0].Target == receipt.Target)
                { _state.Slots[0].Active = false; extraDamage = TacticalInteractionTrialCards.FixedDamage; }
                if (Eligible(1, receipt.Frame) && ledger != null)
                { _state.Slots[1].Active = false; ledger.Grant(TacticalInteractionTrialCards.MomentumGain); }
                if (Eligible(3, receipt.Frame))
                { _state.Slots[3].Active = false; _state.ReturnPending = true; _state.ReturnResult = TacticalReturnResult.WaitingForRelease; }
            }
            if (receipt.Target == _state.Actor && receipt.Attacker != _state.Actor && actualHpLoss > 0 && Eligible(4, receipt.Frame) && ledger != null)
            { _state.Slots[4].Active = false; ledger.Grant(TacticalInteractionTrialCards.MomentumGain); }
            if (receipt.Attacker == _state.Actor && receipt.Target != _state.Actor && ordinaryBlocked && defensePressureAvailable && Eligible(2, receipt.Frame))
            { _state.Slots[2].Active = false; scatterPressure = TacticalInteractionTrialCards.ScatterPressure; }
            return extraDamage;
        }
        internal bool TryGetReturnPoint(long frame, out TacticalWorldPoint point)
        {
            Expire(frame); point = default;
            if (!_state.ReturnPending) return false;
            var slot = _state.Slots[3]; point = new TacticalWorldPoint(slot.X, slot.Y, slot.Z); return true;
        }
        internal void CompleteReturnAttempt(bool success)
        {
            if (!_state.ReturnPending) return;
            _state.ReturnPending = false; _state.ReturnResult = success ? TacticalReturnResult.Returned : TacticalReturnResult.DestinationBlocked;
            var slot = _state.Slots[3]; slot.X = slot.Y = slot.Z = 0; _state.Slots[3] = slot;
        }
        internal TacticalInteractionSnapshot Capture()
        { var value = _state.Clone(); value.Receipts = _receipts.ToArray(); value.Accessories = CaptureAccessories(); return value; }
        internal bool Restore(TacticalInteractionSnapshot snapshot, long frame, GameplayEntityId actor, ITacticalAccessoryAdmissionAuthority admission = null)
        {
            // Existing activations still require their frozen authority when the incoming fragment is omitted.
            if (snapshot == null || !snapshot.IsValid(frame) || (snapshot.Stage != 0 && snapshot.Actor != actor)
                || (admission == null && _accessories.Count != 0)
                || !ValidateAccessoryAdmission(snapshot, admission)) return false;
            _state = snapshot.Clone(); _receipts.Clear(); _receipts.AddRange(snapshot.Receipts); _seen.Clear();
            for (int i = 0; i < snapshot.Receipts.Length; i++) _seen.Add(snapshot.Receipts[i].ToReceipt()); RestoreAccessories(snapshot.Accessories, frame); return true;
        }
        // The unified session owner must validate this join before publishing its fresh world/deck/ledger.
        // This seam owns no second schema, clock, reservation, cooldown or card inventory.
        internal static bool ValidateSessionJoin(TacticalInteractionSnapshot snapshot, TacticalDeckManager deck,
            ulong stage, ulong turn, long frame, GameplayEntityId actor, ITacticalAccessoryAdmissionAuthority admission = null)
        {
            if (snapshot == null || deck == null || !snapshot.IsValid(frame)) return false;
            if (!ValidateAccessoryAdmission(snapshot, admission)) return false;
            if (snapshot.Stage == 0) return true;
            if (snapshot.Stage != stage || snapshot.Turn != turn || snapshot.Actor != actor) return false;
            var identities = new HashSet<ulong>();
            for (int i = 0; i < snapshot.Slots.Length; i++)
            {
                var slot = snapshot.Slots[i]; if (slot.Activation == 0) continue;
                if (!identities.Add(slot.CardInstance) || slot.SourceVersion == uint.MaxValue) return false;
                TacticalCardInstance card = null;
                for (int j = 0; j < deck.ExhaustPile.Count; j++) if (deck.ExhaustPile[j].InstanceId == slot.CardInstance) { card = deck.ExhaustPile[j]; break; }
                if (card == null || card.Zone != CardZone.ExhaustPile || card.Version != slot.SourceVersion + 1
                    || (!card.Definition.HasPublishedBehavior && card.Definition.Id != TacticalInteractionTrialCards.Ids[i])
                    || (card.Definition.HasPublishedBehavior && (card.Definition.PublishedRevision <= 0 || string.IsNullOrWhiteSpace(card.Definition.PublishedContentHash)))
                    || !(card.Definition is TacticalUtilityCardDefinition definition)
                    || !(definition.Payload is InteractionPreparationPayload payload) || (int)payload.Effect != i + 1
                    || definition.BaseMomentumCost != TacticalInteractionTrialCards.Cost || definition.DestinationAfterUse != CardDestination.Exhaust) return false;
            }
            return true;
        }
        internal bool RestoreJoined(TacticalInteractionSnapshot snapshot, TacticalDeckManager deck,
            ulong stage, ulong turn, long frame, GameplayEntityId actor, ITacticalAccessoryAdmissionAuthority admission = null)
            => ValidateSessionJoin(snapshot, deck, stage, turn, frame, actor, admission) && Restore(snapshot, frame, actor, admission);
        // Actor-owner local field keys 325..356; no new global SchemaId or ContributorId is allocated.
        internal void AddHash(ref RuntimeHashAccumulator hash)
        {
            hash.AddLong(TacticalInteractionHashFields.Stage, unchecked((long)_state.Stage)); hash.AddLong(TacticalInteractionHashFields.Turn, unchecked((long)_state.Turn)); hash.AddInt(TacticalInteractionHashFields.ActorIndex, _state.Actor.Index); hash.AddInt(TacticalInteractionHashFields.ActorGeneration, _state.Actor.Generation);
            hash.AddLong(TacticalInteractionHashFields.NextActivation, unchecked((long)_state.NextActivation)); hash.AddInt(TacticalInteractionHashFields.UsedMask, _state.UsedMask); hash.AddLong(TacticalInteractionHashFields.LastReceiptFrame, _state.LastReceiptFrame);
            hash.AddInt(TacticalInteractionHashFields.ReturnPending, _state.ReturnPending ? 1 : 0); hash.AddInt(TacticalInteractionHashFields.ReturnResult, (int)_state.ReturnResult);
            for (int i = 0; i < 5; i++) {
                var slot = _state.Slots[i]; hash.AddInt(TacticalInteractionHashFields.SlotIndex, i); hash.AddInt(TacticalInteractionHashFields.SlotActive, slot.Active ? 1 : 0); hash.AddLong(TacticalInteractionHashFields.SlotActivation, unchecked((long)slot.Activation));
                hash.AddLong(TacticalInteractionHashFields.ArmedFrame, slot.ArmedFrame); hash.AddLong(TacticalInteractionHashFields.ExpiresFrame, slot.ExpiresFrame); hash.AddInt(TacticalInteractionHashFields.TargetIndex, slot.Target.Index); hash.AddInt(TacticalInteractionHashFields.TargetGeneration, slot.Target.Generation);
                hash.AddLong(TacticalInteractionHashFields.CardInstance, unchecked((long)slot.CardInstance)); hash.AddLong(TacticalInteractionHashFields.SourceVersion, slot.SourceVersion); hash.AddInt(TacticalInteractionHashFields.X, slot.X); hash.AddInt(TacticalInteractionHashFields.Y, slot.Y); hash.AddInt(TacticalInteractionHashFields.Z, slot.Z);
            }
            hash.AddInt(TacticalInteractionHashFields.ReceiptCount, _receipts.Count);
            for (int i = 0; i < _receipts.Count; i++) {
                var receipt = _receipts[i]; hash.AddLong(TacticalInteractionHashFields.ReceiptStage, unchecked((long)receipt.Stage)); hash.AddLong(TacticalInteractionHashFields.ReceiptTurn, unchecked((long)receipt.Turn)); hash.AddLong(TacticalInteractionHashFields.ReceiptFrame, receipt.Frame); hash.AddInt(TacticalInteractionHashFields.AttackerIndex, receipt.Attacker.Index); hash.AddInt(TacticalInteractionHashFields.AttackerGeneration, receipt.Attacker.Generation);
                hash.AddInt(TacticalInteractionHashFields.ReceiptTargetIndex, receipt.Target.Index); hash.AddInt(TacticalInteractionHashFields.ReceiptTargetGeneration, receipt.Target.Generation); hash.AddInt(TacticalInteractionHashFields.ActionInstance, receipt.ActionInstance); hash.AddInt(TacticalInteractionHashFields.Trace, receipt.Trace);
                receipt.AddProducerHash(ref hash, TacticalInteractionHashFields.ProducerKind, TacticalInteractionHashFields.ProducerId, TacticalInteractionHashFields.ProducerGeneration, TacticalInteractionHashFields.ProducerOccurrence);
            }
            AddAccessoryHash(ref hash);
        }
    }
}

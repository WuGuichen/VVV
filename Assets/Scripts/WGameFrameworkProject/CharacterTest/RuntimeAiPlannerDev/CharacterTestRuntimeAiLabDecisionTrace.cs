using MxFramework.Runtime;
using System;
using System.Collections.Generic;

namespace MxFramework.Game.CharacterTest
{
    [Flags]
    internal enum CharacterTestRuntimeAiLabDecisionTraceLayer
    {
        None = 0,
        CardArbitration = 1 << 0,
        LogicCondition = 1 << 1,
        MainAction = 1 << 2,
        Fallback = 1 << 3
    }

    internal static class CharacterTestRuntimeAiLabDecisionTraceLayerCatalog
    {
        internal const int LayerCount = 4;

        internal static int IndexOf(CharacterTestRuntimeAiLabDecisionTraceLayer layer)
        {
            switch (layer)
            {
                case CharacterTestRuntimeAiLabDecisionTraceLayer.CardArbitration: return 0;
                case CharacterTestRuntimeAiLabDecisionTraceLayer.LogicCondition: return 1;
                case CharacterTestRuntimeAiLabDecisionTraceLayer.MainAction: return 2;
                case CharacterTestRuntimeAiLabDecisionTraceLayer.Fallback: return 3;
                default: return -1;
            }
        }

        internal static CharacterTestRuntimeAiLabDecisionTraceOverflowRegion OverflowRegion(
            CharacterTestRuntimeAiLabDecisionTraceLayer layer)
        {
            switch (layer)
            {
                case CharacterTestRuntimeAiLabDecisionTraceLayer.CardArbitration:
                    return CharacterTestRuntimeAiLabDecisionTraceOverflowRegion.CardArbitrationCandidates;
                case CharacterTestRuntimeAiLabDecisionTraceLayer.LogicCondition:
                    return CharacterTestRuntimeAiLabDecisionTraceOverflowRegion.LogicConditionCandidates;
                case CharacterTestRuntimeAiLabDecisionTraceLayer.MainAction:
                    return CharacterTestRuntimeAiLabDecisionTraceOverflowRegion.MainActionCandidates;
                case CharacterTestRuntimeAiLabDecisionTraceLayer.Fallback:
                    return CharacterTestRuntimeAiLabDecisionTraceOverflowRegion.FallbackCandidates;
                default:
                    return CharacterTestRuntimeAiLabDecisionTraceOverflowRegion.None;
            }
        }
    }

    internal enum CharacterTestRuntimeAiLabDecisionTraceValueKind
    {
        None,
        Boolean,
        Integer,
        Float,
        Text
    }

    internal enum CharacterTestRuntimeAiLabDecisionTraceAuthorityKind
    {
        Unknown,
        Authority,
        Predicted,
        Derived
    }

    internal enum CharacterTestRuntimeAiLabDecisionTraceFactDomain
    {
        RuntimeAiPlanner,
        BehaviorCardLogic
    }

    internal enum CharacterTestRuntimeAiLabDecisionTraceCommandKind
    {
        None,
        SetControlIntent,
        BehaviorCardIntent
    }

    internal enum CharacterTestRuntimeAiLabDecisionTraceOverflowRegion
    {
        None,
        Agents,
        Facts,
        CardArbitrationCandidates,
        LogicConditionCandidates,
        MainActionCandidates,
        FallbackCandidates,
        CandidateFactLinks
    }

    internal enum CharacterTestRuntimeAiLabDecisionTraceCandidateState
    {
        NotEvaluated,
        Ineligible,
        Suppressed,
        Evaluated,
        Selected
    }

    internal enum CharacterTestRuntimeAiLabPlannerLogicCommitKind
    {
        None,
        Committed,
        Held,
        Pending,
        Rejected,
        Fallback
    }

    internal enum CharacterTestRuntimeAiLabCommandEnqueueKind
    {
        NotRequested,
        Enqueued,
        Rejected
    }

    internal enum CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind
    {
        NotObserved,
        Started,
        Running,
        Completed,
        Cancelled,
        Failed
    }

    internal readonly struct CharacterTestRuntimeAiLabDecisionTraceValue : IEquatable<CharacterTestRuntimeAiLabDecisionTraceValue>
    {
        private CharacterTestRuntimeAiLabDecisionTraceValue(
            CharacterTestRuntimeAiLabDecisionTraceValueKind kind,
            bool boolean,
            int integer,
            float number,
            string text)
        {
            Kind = kind;
            Boolean = boolean;
            Integer = integer;
            Number = number;
            Text = text ?? string.Empty;
            TextDigest = kind == CharacterTestRuntimeAiLabDecisionTraceValueKind.Text ? RuntimeTextContentDigest.Prepare(Text) : default;
        }

        public CharacterTestRuntimeAiLabDecisionTraceValueKind Kind { get; }
        public bool Boolean { get; }
        public int Integer { get; }
        public float Number { get; }
        public string Text { get; }
        public RuntimeHashDigest TextDigest { get; }
        public static CharacterTestRuntimeAiLabDecisionTraceValue Unavailable => default;

        public static CharacterTestRuntimeAiLabDecisionTraceValue FromBoolean(bool value) =>
            new CharacterTestRuntimeAiLabDecisionTraceValue(
                CharacterTestRuntimeAiLabDecisionTraceValueKind.Boolean,
                value,
                0,
                0f,
                string.Empty);

        public static CharacterTestRuntimeAiLabDecisionTraceValue FromInteger(int value) =>
            new CharacterTestRuntimeAiLabDecisionTraceValue(
                CharacterTestRuntimeAiLabDecisionTraceValueKind.Integer,
                false,
                value,
                0f,
                string.Empty);

        public static CharacterTestRuntimeAiLabDecisionTraceValue FromFloat(float value) =>
            new CharacterTestRuntimeAiLabDecisionTraceValue(
                CharacterTestRuntimeAiLabDecisionTraceValueKind.Float,
                false,
                0,
                value,
                string.Empty);

        public static CharacterTestRuntimeAiLabDecisionTraceValue FromText(string value) =>
            new CharacterTestRuntimeAiLabDecisionTraceValue(
                CharacterTestRuntimeAiLabDecisionTraceValueKind.Text,
                false,
                0,
                0f,
                value);

        public bool Equals(CharacterTestRuntimeAiLabDecisionTraceValue other)
        {
            return Kind == other.Kind
                && Boolean == other.Boolean
                && Integer == other.Integer
                && Number.Equals(other.Number)
                && string.Equals(Text, other.Text, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) =>
            obj is CharacterTestRuntimeAiLabDecisionTraceValue other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Kind;
                hash = (hash * 397) ^ (Boolean ? 1 : 0);
                hash = (hash * 397) ^ Integer;
                hash = (hash * 397) ^ Number.GetHashCode();
                hash = (hash * 397) ^ (Text != null ? StringComparer.Ordinal.GetHashCode(Text) : 0);
                return hash;
            }
        }
    }

    internal struct CharacterTestRuntimeAiLabFactObservationTrace
    {
        internal const int MaxDependencies = 2;
        public RuntimeTraceIdentity Identity;
        public RuntimeTraceIdentity SourceIdentity;
        public RuntimeTraceIdentity DependencyIdentity0;
        public RuntimeTraceIdentity DependencyIdentity1;
        public string StableId;
        public CharacterTestRuntimeAiLabDecisionTraceFactDomain Domain;
        public CharacterTestRuntimeAiPlannerFactKind PlannerFact;
        public CharacterTestRuntimeAiLabCardConditionFact Fact;
        public CharacterTestRuntimeAiLabDecisionTraceValue Value;
        public CharacterTestRuntimeAiLabDecisionTraceValue PreviousValue;
        public string SourceStableId;
        public int ObservedFrame;
        public int SourceFrame;
        public long SourceGeneration;
        public int LastChangedFrame;
        public CharacterTestRuntimeAiLabDecisionTraceAuthorityKind AuthorityKind;
        public CharacterTestRuntimeAiLabDecisionTraceLayer ParticipatedLayers;
        public bool BaselineUnknown;
        public bool SourceUnknown;
        public int DerivationCode;
        public int DependencyCount;
        public string DependencyStableId0;
        public string DependencyStableId1;

        public string GetDependencyStableId(int index)
        {
            if (index < 0 || index >= DependencyCount)
                return string.Empty;
            return index == 0
                ? DependencyStableId0 ?? string.Empty
                : DependencyStableId1 ?? string.Empty;
        }

        public RuntimeTraceIdentity GetDependencyIdentity(int index) => index < 0 || index >= DependencyCount ? default : index == 0 ? DependencyIdentity0 : DependencyIdentity1;

        public bool TryAddDependency(string stableId, RuntimeTraceIdentity identity)
        {
            if (identity.IsNone)
                return true;
            for (int i = 0; i < DependencyCount; i++)
            {
                if (GetDependencyIdentity(i).Equals(identity))
                    return true;
            }
            if (DependencyCount >= MaxDependencies)
                return false;
            if (DependencyCount++ == 0)
            {
                DependencyStableId0 = stableId;
                DependencyIdentity0 = identity;
            }
            else
            {
                DependencyStableId1 = stableId;
                DependencyIdentity1 = identity;
            }
            return true;
        }
    }

    internal struct CharacterTestRuntimeAiLabCandidateEvaluationTrace
    {
        internal const int MaxRelatedFacts = 4;
        public RuntimeTraceIdentity Identity;
        public RuntimeTraceIdentity RelatedFactIdentity0;
        public RuntimeTraceIdentity RelatedFactIdentity1;
        public RuntimeTraceIdentity RelatedFactIdentity2;
        public RuntimeTraceIdentity RelatedFactIdentity3;

        public string StableId;
        public CharacterTestRuntimeAiLabDecisionTraceLayer Layer;
        public CharacterTestRuntimeAiLabDecisionTraceCandidateState State;
        public float Score;
        public bool ScoreAvailable;
        public int Priority;
        public bool PriorityAvailable;
        public float Cost;
        public bool CostAvailable;
        public int StableRank;
        public int ReasonCode;
        public int RelatedFactCount;
        public string RelatedFactStableId0;
        public string RelatedFactStableId1;
        public string RelatedFactStableId2;
        public string RelatedFactStableId3;

        public string GetRelatedFactStableId(int index)
        {
            if (index < 0 || index >= RelatedFactCount)
                return string.Empty;
            switch (index)
            {
                case 0: return RelatedFactStableId0 ?? string.Empty;
                case 1: return RelatedFactStableId1 ?? string.Empty;
                case 2: return RelatedFactStableId2 ?? string.Empty;
                case 3: return RelatedFactStableId3 ?? string.Empty;
                default: return string.Empty;
            }
        }

        public RuntimeTraceIdentity GetRelatedFactIdentity(int index)
        {
            if (index < 0 || index >= RelatedFactCount) return default;
            switch (index) { case 0: return RelatedFactIdentity0; case 1: return RelatedFactIdentity1; case 2: return RelatedFactIdentity2; default: return RelatedFactIdentity3; }
        }

        public bool TryAddRelatedFact(string stableId, RuntimeTraceIdentity identity)
        {
            if (identity.IsNone)
                return true;
            for (int i = 0; i < RelatedFactCount; i++)
            {
                if (GetRelatedFactIdentity(i).Equals(identity))
                    return true;
            }
            if (RelatedFactCount >= MaxRelatedFacts)
                return false;
            switch (RelatedFactCount++)
            {
                case 0: RelatedFactStableId0 = stableId; RelatedFactIdentity0 = identity; break;
                case 1: RelatedFactStableId1 = stableId; RelatedFactIdentity1 = identity; break;
                case 2: RelatedFactStableId2 = stableId; RelatedFactIdentity2 = identity; break;
                case 3: RelatedFactStableId3 = stableId; RelatedFactIdentity3 = identity; break;
            }
            return true;
        }
    }

    internal struct CharacterTestRuntimeAiLabDecisionTraceOverflow
    {
        public CharacterTestRuntimeAiLabDecisionTraceOverflowRegion Region;
        public int ActualCount;
        public int Limit;

        public bool IsOverflowed => Region != CharacterTestRuntimeAiLabDecisionTraceOverflowRegion.None;
    }

    internal struct CharacterTestRuntimeAiLabCommittedStateTrace
    {
        public RuntimeTraceIdentity CardIdentity;
        public RuntimeTraceIdentity StageIdentity;
        public RuntimeTraceIdentity MainActionIdentity;
        public RuntimeTraceIdentity PendingTransitionIdentity;
        public string CardStableId;
        public string StageStableId;
        public string MainActionStableId;
        public string PendingTransitionStableId;
    }

    internal struct CharacterTestRuntimeAiLabCommitTrace
    {
        public RuntimeTraceIdentity CardIdentity;
        public RuntimeTraceIdentity StageIdentity;
        public RuntimeTraceIdentity MainActionIdentity;
        public RuntimeTraceIdentity TransitionIdentity;
        public RuntimeTraceIdentity RuntimeEffectActionIdentity;
        public CharacterTestRuntimeAiLabPlannerLogicCommitKind PlannerLogicKind;
        public CharacterTestRuntimeAiLabCommandEnqueueKind CommandEnqueueKind;
        public CharacterTestRuntimeAiLabRuntimeEffectEvidenceKind RuntimeEffectKind;
        public int EvidenceFrame;
        public int PlannerEvidenceFrame;
        public int CommandEvidenceFrame;
        public int RuntimeEffectEvidenceFrame;
        public string CardStableId;
        public string StageStableId;
        public string MainActionStableId;
        public string TransitionStableId;
        public CharacterTestRuntimeAiLabDecisionTraceCommandKind CommandKind;
        public int CommandActionId;
        public int ReasonCode;
        public int PlannerReasonCode;
        public int CommandReasonCode;
        public int RuntimeEffectReasonCode;
        public string RuntimeEffectActionStableId;
    }

    internal readonly struct CharacterTestRuntimeAiLabDecisionTraceHandle : IEquatable<CharacterTestRuntimeAiLabDecisionTraceHandle>
    {
        public CharacterTestRuntimeAiLabDecisionTraceHandle(
            int slot,
            long slotGeneration,
            long sessionGeneration,
            int frame,
            int decisionFrame = -1)
        {
            Slot = slot;
            SlotGeneration = slotGeneration;
            SessionGeneration = sessionGeneration;
            Frame = frame;
            DecisionFrame = decisionFrame >= 0 ? decisionFrame : frame;
        }

        public int Slot { get; }
        public long SlotGeneration { get; }
        public long SessionGeneration { get; }
        public int Frame { get; }
        public int DecisionFrame { get; }
        public bool IsValid => Slot >= 0 && SlotGeneration > 0L && SessionGeneration > 0 && Frame >= 0;

        public bool Equals(CharacterTestRuntimeAiLabDecisionTraceHandle other)
        {
            return Slot == other.Slot
                && SlotGeneration == other.SlotGeneration
                && SessionGeneration == other.SessionGeneration
                && Frame == other.Frame
                && DecisionFrame == other.DecisionFrame;
        }

        public override bool Equals(object obj) =>
            obj is CharacterTestRuntimeAiLabDecisionTraceHandle other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Slot;
                hash = (hash * 397) ^ SlotGeneration.GetHashCode();
                hash = (hash * 397) ^ SessionGeneration.GetHashCode();
                hash = (hash * 397) ^ Frame;
                hash = (hash * 397) ^ DecisionFrame;
                return hash;
            }
        }

        public static CharacterTestRuntimeAiLabDecisionTraceHandle Invalid =>
            new CharacterTestRuntimeAiLabDecisionTraceHandle(-1, 0L, 0, -1);
    }

    internal static class CharacterTestRuntimeAiLabDecisionTraceReasonCodes
    {
        public const int NotEvaluated = CharacterTestAiTraceReason.FACT_CANDIDATE_NOT_EVALUATED;
        public const int ConditionsNotMet = CharacterTestAiTraceReason.FACT_CANDIDATE_CONDITIONS_NOT_MET;
        public const int Selected = CharacterTestAiTraceReason.FACT_CANDIDATE_SELECTED;
        public const int LowerRank = CharacterTestAiTraceReason.FACT_CANDIDATE_LOWER_RANK;
        public const int CommandRejected = CharacterTestAiTraceReason.FACT_COMMAND_ENQUEUE_REJECTED;
        public const int Overflow = CharacterTestAiTraceReason.FACT_TRACE_OVERFLOW;
    }

    internal static class CharacterTestRuntimeAiLabFactCatalog
    {
        private static readonly string[] StableIds =
        {
            "character-test.runtime-ai-planner.target.exists",
            "character-test.runtime-ai-planner.target.visible",
            "character-test.runtime-ai-planner.target.distance",
            "character-test.runtime-ai-planner.self.actionRunning",
            "character-test.runtime-ai-planner.self.canTransitionToAction",
            "character-test.runtime-ai-planner.ability.available",
            "character-test.runtime-ai-planner.move.pathAvailable",
            "character-test.runtime-ai-planner.target.attackWillHitSelf",
            "character-test.runtime-ai-planner.target.attackImpactFrames",
            "character-test.runtime-ai-planner.target.attackInstanceResponded",
            "character-test.runtime-ai-planner.self.dodgeCooldownReady",
            "character-test.runtime-ai-planner.target.inAttackRange",
            "character-test.runtime-ai-planner.target.lineOfSightBlocked",
            "character-test.runtime-ai-planner.environment.nearHazard",
            "character-test.runtime-ai-planner.target.dead",
            "character-test.runtime-ai-planner.target.distanceAvailable",
            "character-test.runtime-ai-planner.target.rootDistance",
            "character-test.runtime-ai-planner.target.state-mask",
            "character-test.runtime-ai-planner.self.actionInterruptible",
            "character-test.runtime-ai-planner.self.healthRatio"
        };

        public static int Count => StableIds.Length;

        public static CharacterTestRuntimeAiLabCardConditionFact KindAt(int index) =>
            index >= 0 && index < Count
                ? (CharacterTestRuntimeAiLabCardConditionFact)index
                : CharacterTestRuntimeAiLabCardConditionFact.TargetExists;

        public static string StableId(CharacterTestRuntimeAiLabCardConditionFact fact)
        {
            int index = (int)fact;
            return index >= 0 && index < StableIds.Length ? StableIds[index] : string.Empty;
        }

        public static string SourceStableId(CharacterTestRuntimeAiLabCardConditionFact fact)
        {
            switch (fact)
            {
                case CharacterTestRuntimeAiLabCardConditionFact.SelfHealthRatio:
                    return "character-test.combat.health";
                case CharacterTestRuntimeAiLabCardConditionFact.SelfCanTransitionToAction:
                case CharacterTestRuntimeAiLabCardConditionFact.SelfActionInterruptible:
                    return "character-action.runtime";
                case CharacterTestRuntimeAiLabCardConditionFact.AbilityAvailable:
                    return "ability.runtime";
                default:
                    return "character-test.runtime-ai-planner.sense";
            }
        }

        public static int DerivationCode(CharacterTestRuntimeAiLabCardConditionFact fact)
        {
            switch (fact)
            {
                case CharacterTestRuntimeAiLabCardConditionFact.SelfHealthRatio:
                    return CharacterTestAiTraceReason.FACT_DERIVE_SELF_HEALTH_RATIO;
                case CharacterTestRuntimeAiLabCardConditionFact.SelfCanTransitionToAction:
                    return CharacterTestAiTraceReason.FACT_DERIVE_CHARACTER_ACTION_TRANSITION;
                case CharacterTestRuntimeAiLabCardConditionFact.SelfActionInterruptible:
                    return CharacterTestAiTraceReason.FACT_DERIVE_CHARACTER_ACTION_CANCEL_WINDOW;
                case CharacterTestRuntimeAiLabCardConditionFact.AbilityAvailable:
                    return CharacterTestAiTraceReason.FACT_DERIVE_ABILITY_AVAILABILITY;
                default:
                    return CharacterTestAiTraceReason.FACT_DERIVE_PLANNER_SENSE;
            }
        }

        public static CharacterTestRuntimeAiLabDecisionTraceAuthorityKind AuthorityKind(
            CharacterTestRuntimeAiLabCardConditionFact fact)
        {
            return CharacterTestRuntimeAiLabDecisionTraceAuthorityKind.Derived;
        }

        public static ulong Mask(CharacterTestRuntimeAiLabCardConditionFact fact)
        {
            int bit = (int)fact;
            return bit >= 0 && bit < 64 ? 1UL << bit : 0UL;
        }

        public static CharacterTestRuntimeAiLabDecisionTraceValue ReadValue(
            CharacterTestRuntimeAiLabCardConditionFact fact,
            CharacterTestRuntimeAiPlannerFactState facts)
        {
            if (facts == null)
                return CharacterTestRuntimeAiLabDecisionTraceValue.Unavailable;
            switch (fact)
            {
                case CharacterTestRuntimeAiLabCardConditionFact.SelfHealthRatio:
                    return float.IsNaN(facts.SelfHealthRatio) ? CharacterTestRuntimeAiLabDecisionTraceValue.Unavailable
                        : CharacterTestRuntimeAiLabDecisionTraceValue.FromFloat(facts.SelfHealthRatio);
                case CharacterTestRuntimeAiLabCardConditionFact.TargetExists:
                    return CharacterTestRuntimeAiLabDecisionTraceValue.FromBoolean(facts.TargetExists);
                case CharacterTestRuntimeAiLabCardConditionFact.TargetVisible:
                    return CharacterTestRuntimeAiLabDecisionTraceValue.FromBoolean(facts.TargetVisible);
                case CharacterTestRuntimeAiLabCardConditionFact.TargetDead:
                    return CharacterTestRuntimeAiLabDecisionTraceValue.FromBoolean(facts.TargetDead);
                case CharacterTestRuntimeAiLabCardConditionFact.TargetState:
                    return CharacterTestRuntimeAiLabDecisionTraceValue.FromInteger(
                        (int)facts.TargetStateMask);
                case CharacterTestRuntimeAiLabCardConditionFact.TargetDistance:
                    return CharacterTestRuntimeAiLabDecisionTraceValue.FromFloat(facts.TargetDistance);
                case CharacterTestRuntimeAiLabCardConditionFact.TargetDistanceAvailable:
                    return CharacterTestRuntimeAiLabDecisionTraceValue.FromBoolean(
                        facts.TargetDistanceAvailable);
                case CharacterTestRuntimeAiLabCardConditionFact.TargetRootDistance:
                    return CharacterTestRuntimeAiLabDecisionTraceValue.FromFloat(
                        facts.TargetRootDistance);
                case CharacterTestRuntimeAiLabCardConditionFact.SelfActionRunning:
                    return CharacterTestRuntimeAiLabDecisionTraceValue.FromBoolean(facts.SelfActionRunning);
                case CharacterTestRuntimeAiLabCardConditionFact.SelfActionInterruptible:
                    return CharacterTestRuntimeAiLabDecisionTraceValue.FromBoolean(
                        facts.SelfActionInterruptible);
                case CharacterTestRuntimeAiLabCardConditionFact.MovePathAvailable:
                    return facts.NavigationObserved ? CharacterTestRuntimeAiLabDecisionTraceValue.FromBoolean(facts.MovePathAvailable) : CharacterTestRuntimeAiLabDecisionTraceValue.Unavailable;
                case CharacterTestRuntimeAiLabCardConditionFact.TargetAttackWillHitSelf:
                    return CharacterTestRuntimeAiLabDecisionTraceValue.FromBoolean(facts.TargetAttackWillHitSelf);
                case CharacterTestRuntimeAiLabCardConditionFact.TargetAttackImpactFrames:
                    return CharacterTestRuntimeAiLabDecisionTraceValue.FromInteger(facts.TargetAttackImpactFrames);
                case CharacterTestRuntimeAiLabCardConditionFact.TargetAttackInstanceResponded:
                    return CharacterTestRuntimeAiLabDecisionTraceValue.FromBoolean(facts.TargetAttackInstanceResponded);
                case CharacterTestRuntimeAiLabCardConditionFact.SelfDodgeCooldownReady:
                    return CharacterTestRuntimeAiLabDecisionTraceValue.FromBoolean(facts.SelfDodgeCooldownReady);
                case CharacterTestRuntimeAiLabCardConditionFact.TargetInAttackRange:
                    return facts.PrimaryAttackFactObserved
                        ? CharacterTestRuntimeAiLabDecisionTraceValue.FromBoolean(facts.TargetInAttackRange)
                        : CharacterTestRuntimeAiLabDecisionTraceValue.Unavailable;
                case CharacterTestRuntimeAiLabCardConditionFact.TargetLineOfSightBlocked:
                    return CharacterTestRuntimeAiLabDecisionTraceValue.FromBoolean(facts.TargetLineOfSightBlocked);
                case CharacterTestRuntimeAiLabCardConditionFact.EnvironmentNearHazard:
                    return facts.EnvironmentObserved ? CharacterTestRuntimeAiLabDecisionTraceValue.FromBoolean(facts.EnvironmentNearHazard) : CharacterTestRuntimeAiLabDecisionTraceValue.Unavailable;
                default:
                    return CharacterTestRuntimeAiLabDecisionTraceValue.Unavailable;
            }
        }
    }

    internal readonly struct CharacterTestRuntimeAiLabDecisionTraceAgentView
    {
        private readonly CharacterTestRuntimeAiLabDecisionTraceStore.AgentSlot _slot;

        internal CharacterTestRuntimeAiLabDecisionTraceAgentView(
            CharacterTestRuntimeAiLabDecisionTraceStore.AgentSlot slot)
        {
            _slot = slot;
        }

        public bool IsValid => _slot != null && _slot.EntityId > 0;
        public int EntityId => _slot?.EntityId ?? 0;
        public CharacterTestRuntimeAiLabCommittedStateTrace PreviousCommitted =>
            _slot != null ? _slot.PreviousCommitted : default;
        public CharacterTestRuntimeAiLabCommitTrace Commit => _slot != null ? _slot.Commit : default;
        public IReadOnlyList<CharacterTestRuntimeAiLabFactObservationTrace> Facts =>
            _slot != null ? _slot.FactView : Array.Empty<CharacterTestRuntimeAiLabFactObservationTrace>();
        public IReadOnlyList<CharacterTestRuntimeAiLabCandidateEvaluationTrace> Candidates =>
            _slot != null ? _slot.CandidateView : Array.Empty<CharacterTestRuntimeAiLabCandidateEvaluationTrace>();
        public bool Overflowed => _slot != null && _slot.Overflowed;
        public CharacterTestRuntimeAiLabDecisionTraceOverflow Overflow =>
            _slot != null ? _slot.Overflow : default;
    }

    /// <summary>
    /// Preallocated presentation-diagnostics arena for retained Runtime AI Lab frames.
    /// </summary>
    /// <remarks>
    /// NoAlloc after construction on Capture/Release/TryGet. Clone is LowFreqAlloc and is reserved for
    /// E2 recovery snapshots. Contents never contribute to runtime authority or Replay hashes.
    /// </remarks>
    internal sealed class CharacterTestRuntimeAiLabDecisionTraceStore
    {
        internal const int DefaultMaxAgents = 8;
        internal const int DefaultMaxFactsPerAgent = 32;
        internal const int DefaultMaxCandidatesPerLayer = 16;
        private const int RecoveryHeadroom = 4;

        private readonly FrameSlot[] _slots;
        private readonly int[] _freeSlots;
        private int _freeCount;
        private long _nextGeneration = 1L;

        public CharacterTestRuntimeAiLabDecisionTraceStore(
            int retainedFrameCapacity,
            int maxAgents = DefaultMaxAgents,
            int maxFactsPerAgent = DefaultMaxFactsPerAgent,
            int maxCandidatesPerLayer = DefaultMaxCandidatesPerLayer)
        {
            int slotCount = Math.Max(1, retainedFrameCapacity) + RecoveryHeadroom;
            int safeAgents = Math.Max(1, maxAgents);
            int safeFacts = Math.Max(1, maxFactsPerAgent);
            int safeCandidates = Math.Max(1, maxCandidatesPerLayer);
            _slots = new FrameSlot[slotCount];
            _freeSlots = new int[slotCount];
            for (int i = 0; i < slotCount; i++)
            {
                _slots[i] = new FrameSlot(safeAgents, safeFacts, safeCandidates);
                _freeSlots[i] = slotCount - 1 - i;
            }
            _freeCount = slotCount;
        }

        private CharacterTestRuntimeAiLabDecisionTraceStore(
            FrameSlot[] slots,
            int[] freeSlots,
            int freeCount,
            long nextGeneration)
        {
            _slots = slots;
            _freeSlots = freeSlots;
            _freeCount = freeCount;
            _nextGeneration = nextGeneration;
        }

        public int Capacity => _slots.Length - RecoveryHeadroom;

        public CharacterTestRuntimeAiLabDecisionTraceWriter BeginCapture(
            long sessionGeneration,
            int frame,
            out CharacterTestRuntimeAiLabDecisionTraceHandle handle,
            bool staging = false,
            int decisionFrame = -1)
        {
            if (sessionGeneration <= 0 || frame < 0 || _freeCount <= 0)
            {
                handle = CharacterTestRuntimeAiLabDecisionTraceHandle.Invalid;
                return default;
            }

            int slotIndex = _freeSlots[--_freeCount];
            FrameSlot slot = _slots[slotIndex];
            long generation = NextGeneration();
            int resolvedDecisionFrame = decisionFrame >= 0 ? decisionFrame : frame;
            slot.Reset(sessionGeneration, frame, resolvedDecisionFrame, generation, staging);
            handle = new CharacterTestRuntimeAiLabDecisionTraceHandle(
                slotIndex,
                generation,
                sessionGeneration,
                frame,
                resolvedDecisionFrame);
            return new CharacterTestRuntimeAiLabDecisionTraceWriter(slot);
        }

        public void Release(CharacterTestRuntimeAiLabDecisionTraceHandle handle)
        {
            if (!TryResolve(handle, out FrameSlot slot))
                return;
            if (slot.PinCount > 0)
            {
                slot.PendingRelease = true;
                return;
            }
            ReleaseResolved(handle.Slot, slot);
        }

        public bool Pin(CharacterTestRuntimeAiLabDecisionTraceHandle handle)
        {
            if (!TryResolve(handle, out FrameSlot slot))
                return false;
            slot.PinCount++;
            return true;
        }

        public bool Unpin(CharacterTestRuntimeAiLabDecisionTraceHandle handle)
        {
            if (!TryResolve(handle, out FrameSlot slot) || slot.PinCount <= 0)
                return false;
            slot.PinCount--;
            if (slot.PinCount == 0 && slot.PendingRelease)
                ReleaseResolved(handle.Slot, slot);
            return true;
        }

        public bool PromoteStaging(CharacterTestRuntimeAiLabDecisionTraceHandle handle)
        {
            if (!TryResolve(handle, out FrameSlot slot) || !slot.Staging)
                return false;
            slot.Staging = false;
            return true;
        }

        internal long ComputeRecoveryHash()
        {
            unchecked
            {
                ulong hash = 1469598103934665603UL;
                AddHash(ref hash, _freeCount);
                AddHash(ref hash, _nextGeneration);
                for (int i = 0; i < _freeCount; i++)
                    AddHash(ref hash, _freeSlots[i]);
                for (int i = 0; i < _slots.Length; i++)
                {
                    FrameSlot slot = _slots[i];
                    AddHash(ref hash, i);
                    AddHash(ref hash, slot.InUse ? 1 : 0);
                    AddHash(ref hash, slot.Sealed ? 1 : 0);
                    AddHash(ref hash, slot.Staging ? 1 : 0);
                    AddHash(ref hash, slot.PendingRelease ? 1 : 0);
                    AddHash(ref hash, slot.PinCount);
                    AddHash(ref hash, slot.SessionGeneration);
                    AddHash(ref hash, slot.Frame);
                    AddHash(ref hash, slot.DecisionFrame);
                    AddHash(ref hash, slot.Generation);
                }
                return (long)hash;
            }
        }

        private void ReleaseResolved(int slotIndex, FrameSlot slot)
        {
            slot.Release();
            _freeSlots[_freeCount++] = slotIndex;
        }

        public bool TryGetAgent(
            CharacterTestRuntimeAiLabDecisionTraceHandle handle,
            int entityId,
            out CharacterTestRuntimeAiLabDecisionTraceAgentView view)
        {
            view = default;
            if (!TryResolve(handle, out FrameSlot slot) || !slot.Sealed || slot.Staging)
                return false;
            for (int i = 0; i < slot.AgentCount; i++)
            {
                AgentSlot agent = slot.Agents[i];
                if (agent.EntityId != entityId)
                    continue;
                view = new CharacterTestRuntimeAiLabDecisionTraceAgentView(agent);
                return true;
            }
            return false;
        }

        public CharacterTestRuntimeAiLabDecisionTraceStore Clone()
        {
            var slots = new FrameSlot[_slots.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                // Free (non-InUse) slots share AgentSlot references to avoid allocating
                // thousands of empty arrays. InUse slots are fully deep-cloned so their
                // captured agent data remains independent of the original store.
                slots[i] = _slots[i].InUse
                    ? _slots[i].Clone()
                    : _slots[i].CloneShared();
            }
            var free = new int[_freeSlots.Length];
            Array.Copy(_freeSlots, free, free.Length);
            return new CharacterTestRuntimeAiLabDecisionTraceStore(slots, free, _freeCount, _nextGeneration);
        }


        private bool TryResolve(
            CharacterTestRuntimeAiLabDecisionTraceHandle handle,
            out FrameSlot slot)
        {
            slot = null;
            if (!handle.IsValid || handle.Slot < 0 || handle.Slot >= _slots.Length)
                return false;
            FrameSlot candidate = _slots[handle.Slot];
            if (!candidate.InUse
                || candidate.Generation != handle.SlotGeneration
                || candidate.SessionGeneration != handle.SessionGeneration
                || candidate.Frame != handle.Frame
                || candidate.DecisionFrame != handle.DecisionFrame)
            {
                return false;
            }
            slot = candidate;
            return true;
        }

        private long NextGeneration()
        {
            long value = _nextGeneration++;
            if (_nextGeneration <= 0L)
                _nextGeneration = 1L;
            return value > 0L ? value : 1L;
        }

        private static void AddHash(ref ulong hash, long value)
        {
            unchecked
            {
                hash ^= (ulong)value;
                hash *= 1099511628211UL;
            }
        }

        internal sealed class FrameSlot
        {
            public FrameSlot(int maxAgents, int maxFacts, int maxCandidates)
            {
                Agents = new AgentSlot[maxAgents];
                for (int i = 0; i < Agents.Length; i++)
                    Agents[i] = new AgentSlot(maxFacts, maxCandidates);
            }

            /// <summary>
            /// Constructs a FrameSlot that shares the supplied AgentSlot array without
            /// allocating new AgentSlot objects. Used by <see cref="CloneShared"/> for
            /// free (non-InUse) slots where agent data is irrelevant.
            /// </summary>
            private FrameSlot(AgentSlot[] sharedAgents)
            {
                Agents = sharedAgents;
            }

            public bool InUse { get; private set; }
            public bool Sealed { get; private set; }
            public bool Staging { get; set; }
            public bool PendingRelease { get; set; }
            public int PinCount { get; set; }
            public long SessionGeneration { get; private set; }
            public int Frame { get; private set; }
            public int DecisionFrame { get; private set; }
            public long Generation { get; private set; }
            public AgentSlot[] Agents { get; }
            public int AgentCount { get; private set; }
            public bool Overflowed { get; private set; }
            public CharacterTestRuntimeAiLabDecisionTraceOverflow Overflow { get; private set; }

            public void Reset(
                long sessionGeneration,
                int frame,
                int decisionFrame,
                long generation,
                bool staging)
            {
                InUse = true;
                Sealed = false;
                Staging = staging;
                PendingRelease = false;
                PinCount = 0;
                SessionGeneration = sessionGeneration;
                Frame = frame;
                DecisionFrame = decisionFrame;
                Generation = generation;
                AgentCount = 0;
                Overflowed = false;
                Overflow = default;
                for (int i = 0; i < Agents.Length; i++)
                    Agents[i].Reset();
            }

            public AgentSlot GetOrBeginAgent(int entityId)
            {
                if (Sealed || entityId <= 0)
                    return null;
                for (int i = 0; i < AgentCount; i++)
                {
                    if (Agents[i].EntityId == entityId)
                        return Agents[i];
                }
                if (AgentCount >= Agents.Length)
                {
                    Overflowed = true;
                    Overflow = new CharacterTestRuntimeAiLabDecisionTraceOverflow
                    {
                        Region = CharacterTestRuntimeAiLabDecisionTraceOverflowRegion.Agents,
                        ActualCount = AgentCount + 1,
                        Limit = Agents.Length
                    };
                    return null;
                }
                AgentSlot agent = Agents[AgentCount++];
                agent.EntityId = entityId;
                return agent;
            }

            public void Seal()
            {
                if (InUse)
                    Sealed = true;
            }

            public void Release()
            {
                InUse = false;
                Sealed = false;
                Staging = false;
                PendingRelease = false;
                PinCount = 0;
                SessionGeneration = 0;
                Frame = -1;
                DecisionFrame = -1;
                AgentCount = 0;
                Overflowed = false;
                Overflow = default;
            }

            /// <summary>
            /// Full deep clone: allocates new AgentSlot objects and copies all agent data.
            /// Use for <see cref="InUse"/> slots where agent data must be preserved independently.
            /// </summary>
            public FrameSlot Clone()
            {
                int facts = Agents.Length > 0 ? Agents[0].Facts.Length : 1;
                int candidates = Agents.Length > 0
                    ? Math.Max(1, Agents[0].MaxCandidatesPerLayer)
                    : 1;
                var clone = new FrameSlot(Agents.Length, facts, candidates)
                {
                    InUse = InUse,
                    Sealed = Sealed,
                    Staging = Staging,
                    PendingRelease = PendingRelease,
                    PinCount = PinCount,
                    SessionGeneration = SessionGeneration,
                    Frame = Frame,
                    DecisionFrame = DecisionFrame,
                    Generation = Generation,
                    AgentCount = AgentCount,
                    Overflowed = Overflowed
                };
                clone.Overflow = Overflow;
                for (int i = 0; i < Agents.Length; i++)
                    clone.Agents[i].CopyFrom(Agents[i]);
                return clone;
            }

            /// <summary>
            /// Shallow clone: shares the original AgentSlot array references without
            /// allocating new AgentSlot objects. Safe for free (!<see cref="InUse"/>) slots
            /// because their agent data is always in Reset state and irrelevant; the caller
            /// guarantees the original store will not write to these agents concurrently.
            /// </summary>
            internal FrameSlot CloneShared()
            {
                var clone = new FrameSlot(Agents)
                {
                    InUse = InUse,         // false for free slots
                    Sealed = Sealed,
                    Staging = Staging,
                    PendingRelease = PendingRelease,
                    PinCount = PinCount,
                    SessionGeneration = SessionGeneration,
                    Frame = Frame,
                    DecisionFrame = DecisionFrame,
                    Generation = Generation,
                    AgentCount = AgentCount,
                    Overflowed = Overflowed
                };
                clone.Overflow = Overflow;
                return clone;
            }
        }


        internal sealed class AgentSlot
        {
            public AgentSlot(int maxFacts, int maxCandidates)
            {
                Facts = new CharacterTestRuntimeAiLabFactObservationTrace[maxFacts];
                Candidates = new CharacterTestRuntimeAiLabCandidateEvaluationTrace[
                    maxCandidates * CharacterTestRuntimeAiLabDecisionTraceLayerCatalog.LayerCount];
                CandidateLayerCounts = new int[CharacterTestRuntimeAiLabDecisionTraceLayerCatalog.LayerCount];
                MaxCandidatesPerLayer = maxCandidates;
                FactView = new CountedReadOnlyList<CharacterTestRuntimeAiLabFactObservationTrace>(Facts);
                CandidateView = new CountedReadOnlyList<CharacterTestRuntimeAiLabCandidateEvaluationTrace>(Candidates);
            }

            public int EntityId;
            public CharacterTestRuntimeAiLabCommittedStateTrace PreviousCommitted;
            public CharacterTestRuntimeAiLabCommitTrace Commit;
            public CharacterTestRuntimeAiLabFactObservationTrace[] Facts { get; }
            public CharacterTestRuntimeAiLabCandidateEvaluationTrace[] Candidates { get; }
            public CountedReadOnlyList<CharacterTestRuntimeAiLabFactObservationTrace> FactView { get; }
            public CountedReadOnlyList<CharacterTestRuntimeAiLabCandidateEvaluationTrace> CandidateView { get; }
            public int FactCount;
            public int CandidateCount;
            public bool Overflowed;
            public CharacterTestRuntimeAiLabDecisionTraceOverflow Overflow;
            public int[] CandidateLayerCounts { get; }
            public int MaxCandidatesPerLayer { get; }
            public bool Sealed;

            public void Reset()
            {
                EntityId = 0;
                PreviousCommitted = default;
                Commit = default;
                FactCount = 0;
                CandidateCount = 0;
                Overflowed = false;
                Overflow = default;
                Array.Clear(CandidateLayerCounts, 0, CandidateLayerCounts.Length);
                Sealed = false;
                FactView.SetCount(0);
                CandidateView.SetCount(0);
            }

            public void CopyFrom(AgentSlot source)
            {
                EntityId = source.EntityId;
                PreviousCommitted = source.PreviousCommitted;
                Commit = source.Commit;
                FactCount = source.FactCount;
                CandidateCount = source.CandidateCount;
                Overflowed = source.Overflowed;
                Overflow = source.Overflow;
                Array.Copy(source.CandidateLayerCounts, CandidateLayerCounts, CandidateLayerCounts.Length);
                Sealed = source.Sealed;
                Array.Copy(source.Facts, Facts, source.FactCount);
                Array.Copy(source.Candidates, Candidates, source.CandidateCount);
                FactView.SetCount(FactCount);
                CandidateView.SetCount(CandidateCount);
            }

            public void SealViews()
            {
                FactView.SetCount(FactCount);
                CandidateView.SetCount(CandidateCount);
                Sealed = true;
            }

            public void RecordOverflow(
                CharacterTestRuntimeAiLabDecisionTraceOverflowRegion region,
                int actualCount,
                int limit)
            {
                Overflowed = true;
                if (!Overflow.IsOverflowed)
                {
                    Overflow = new CharacterTestRuntimeAiLabDecisionTraceOverflow
                    {
                        Region = region,
                        ActualCount = actualCount,
                        Limit = limit
                    };
                }
            }
        }

        internal sealed class CountedReadOnlyList<T> : IReadOnlyList<T>
        {
            private readonly T[] _items;

            public CountedReadOnlyList(T[] items)
            {
                _items = items ?? Array.Empty<T>();
            }

            public int Count { get; private set; }

            internal void SetCount(int count)
            {
                Count = count >= 0 && count <= _items.Length ? count : 0;
            }
            public T this[int index]
            {
                get
                {
                    if (index < 0 || index >= Count)
                        throw new ArgumentOutOfRangeException(nameof(index));
                    return _items[index];
                }
            }

            public IEnumerator<T> GetEnumerator()
            {
                for (int i = 0; i < Count; i++)
                    yield return _items[i];
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }

    internal readonly struct CharacterTestRuntimeAiLabDecisionTraceWriter
    {
        private readonly CharacterTestRuntimeAiLabDecisionTraceStore.FrameSlot _slot;

        internal CharacterTestRuntimeAiLabDecisionTraceWriter(
            CharacterTestRuntimeAiLabDecisionTraceStore.FrameSlot slot)
        {
            _slot = slot;
        }

        public bool IsValid => _slot != null;

        public CharacterTestRuntimeAiLabDecisionTraceAgentWriter BeginAgent(int entityId)
        {
            return new CharacterTestRuntimeAiLabDecisionTraceAgentWriter(_slot?.GetOrBeginAgent(entityId));
        }

        public void Seal()
        {
            if (_slot == null)
                return;
            for (int i = 0; i < _slot.AgentCount; i++)
                _slot.Agents[i].SealViews();
            _slot.Seal();
        }
    }

    internal readonly struct CharacterTestRuntimeAiLabDecisionTraceAgentWriter
    {
        private readonly CharacterTestRuntimeAiLabDecisionTraceStore.AgentSlot _slot;

        internal CharacterTestRuntimeAiLabDecisionTraceAgentWriter(
            CharacterTestRuntimeAiLabDecisionTraceStore.AgentSlot slot)
        {
            _slot = slot;
        }

        public bool IsValid => _slot != null;

        public CharacterTestRuntimeAiLabCommittedStateTrace CurrentPrevious =>
            _slot != null ? _slot.PreviousCommitted : default;

        public CharacterTestRuntimeAiLabCommitTrace CurrentCommit =>
            _slot != null ? _slot.Commit : default;

        public void SetPrevious(in CharacterTestRuntimeAiLabCommittedStateTrace value)
        {
            if (_slot != null && !_slot.Sealed)
                _slot.PreviousCommitted = value;
        }

        public void SetCommit(in CharacterTestRuntimeAiLabCommitTrace value)
        {
            if (_slot != null && !_slot.Sealed)
                _slot.Commit = value;
        }

        public bool AddFact(in CharacterTestRuntimeAiLabFactObservationTrace value)
        {
            if (_slot == null || _slot.Sealed)
                return false;
            for (int i = 0; i < _slot.FactCount; i++)
            {
                if (_slot.Facts[i].Identity.Equals(value.Identity))
                {
                    _slot.RecordOverflow(
                        CharacterTestRuntimeAiLabDecisionTraceOverflowRegion.Facts,
                        _slot.FactCount + 1,
                        _slot.Facts.Length);
                    return false;
                }
            }
            if (_slot.FactCount >= _slot.Facts.Length)
            {
                _slot.RecordOverflow(
                    CharacterTestRuntimeAiLabDecisionTraceOverflowRegion.Facts,
                    _slot.FactCount + 1,
                    _slot.Facts.Length);
                return false;
            }
            _slot.Facts[_slot.FactCount++] = value;
            return true;
        }

        public bool AddCandidate(in CharacterTestRuntimeAiLabCandidateEvaluationTrace value)
        {
            if (_slot == null || _slot.Sealed)
                return false;
            int layerIndex = CharacterTestRuntimeAiLabDecisionTraceLayerCatalog.IndexOf(value.Layer);
            if (layerIndex < 0)
                return false;
            for (int i = 0; i < _slot.CandidateCount; i++)
            {
                if (_slot.Candidates[i].Layer == value.Layer
                    && _slot.Candidates[i].Identity.Equals(value.Identity))
                {
                    _slot.RecordOverflow(
                        CharacterTestRuntimeAiLabDecisionTraceLayerCatalog.OverflowRegion(value.Layer),
                        _slot.CandidateLayerCounts[layerIndex] + 1,
                        _slot.MaxCandidatesPerLayer);
                    return false;
                }
            }
            if (_slot.CandidateLayerCounts[layerIndex] >= _slot.MaxCandidatesPerLayer
                || _slot.CandidateCount >= _slot.Candidates.Length)
            {
                _slot.RecordOverflow(
                    CharacterTestRuntimeAiLabDecisionTraceLayerCatalog.OverflowRegion(value.Layer),
                    _slot.CandidateLayerCounts[layerIndex] + 1,
                    _slot.MaxCandidatesPerLayer);
                return false;
            }
            _slot.Candidates[_slot.CandidateCount++] = value;
            _slot.CandidateLayerCounts[layerIndex]++;
            return true;
        }

        public void RecordCandidateFactLinkOverflow()
        {
            _slot?.RecordOverflow(
                CharacterTestRuntimeAiLabDecisionTraceOverflowRegion.CandidateFactLinks,
                CharacterTestRuntimeAiLabCandidateEvaluationTrace.MaxRelatedFacts + 1,
                CharacterTestRuntimeAiLabCandidateEvaluationTrace.MaxRelatedFacts);
        }

        public void RecordOverflow(
            CharacterTestRuntimeAiLabDecisionTraceOverflowRegion region,
            int actualCount,
            int limit)
        {
            _slot?.RecordOverflow(region, actualCount, limit);
        }
    }
}

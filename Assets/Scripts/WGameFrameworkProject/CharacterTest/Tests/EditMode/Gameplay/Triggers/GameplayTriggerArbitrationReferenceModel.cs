using System;
using System.Collections.Generic;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers;

namespace MxFramework.Tests.Gameplay.Triggers
{
    internal enum GameplayTriggerArbitrationReferenceDecision
    {
        Accepted = 0,
        Suppressed = 1,
        MissingScopeEntity = 2,
        Ineligible = 3
    }

    internal readonly struct GameplayTriggerArbitrationReferenceCandidate
    {
        public GameplayTriggerArbitrationReferenceCandidate(
            GameplayTriggerDefinitionId definitionId,
            GameplayTriggerRouteId routeId,
            in GameplayTriggerReferenceOccurrence occurrence,
            int executionOrder = 0,
            int exclusiveGroupId = 0,
            int winnerPriority = 0,
            GameplayTriggerArbitrationScope arbitrationScope = GameplayTriggerArbitrationScope.Global,
            bool eligible = true)
        {
            DefinitionId = definitionId;
            RouteId = routeId;
            Occurrence = occurrence;
            ExecutionOrder = executionOrder;
            ExclusiveGroupId = exclusiveGroupId;
            WinnerPriority = winnerPriority;
            ArbitrationScope = arbitrationScope;
            Eligible = eligible;
        }

        public GameplayTriggerDefinitionId DefinitionId { get; }
        public GameplayTriggerRouteId RouteId { get; }
        public GameplayTriggerReferenceOccurrence Occurrence { get; }
        public int ExecutionOrder { get; }
        public int ExclusiveGroupId { get; }
        public int WinnerPriority { get; }
        public GameplayTriggerArbitrationScope ArbitrationScope { get; }
        public bool Eligible { get; }
    }

    internal readonly struct GameplayTriggerArbitrationReferenceResult
    {
        public GameplayTriggerArbitrationReferenceResult(
            in GameplayTriggerArbitrationReferenceCandidate candidate,
            GameplayTriggerArbitrationReferenceDecision decision)
        {
            Candidate = candidate;
            Decision = decision;
        }

        public GameplayTriggerArbitrationReferenceCandidate Candidate { get; }
        public GameplayTriggerArbitrationReferenceDecision Decision { get; }
    }

    /// <summary>
    /// Test-only arbitration oracle. It intentionally uses lists and explicit group scans
    /// instead of the production candidate buffers and in-place insertion sorts.
    /// </summary>
    internal sealed class GameplayTriggerArbitrationReferenceModel
    {
        private readonly List<GameplayTriggerArbitrationReferenceResult> _results =
            new List<GameplayTriggerArbitrationReferenceResult>();
        private readonly List<GameplayTriggerArbitrationReferenceCandidate> _accepted =
            new List<GameplayTriggerArbitrationReferenceCandidate>();

        public IReadOnlyList<GameplayTriggerArbitrationReferenceResult> Results => _results;
        public IReadOnlyList<GameplayTriggerArbitrationReferenceCandidate> Accepted => _accepted;

        public void Resolve(IReadOnlyList<GameplayTriggerArbitrationReferenceCandidate> candidates)
        {
            _results.Clear();
            _accepted.Clear();
            for (int i = 0; i < candidates.Count; i++)
            {
                GameplayTriggerArbitrationReferenceCandidate candidate = candidates[i];
                GameplayTriggerArbitrationReferenceDecision decision = ResolveCandidate(candidates, i);
                _results.Add(new GameplayTriggerArbitrationReferenceResult(candidate, decision));
                if (decision == GameplayTriggerArbitrationReferenceDecision.Accepted)
                {
                    _accepted.Add(candidate);
                }
            }
            _accepted.Sort(CompareExecution);
            _results.Sort(CompareResultIdentity);
        }

        private static GameplayTriggerArbitrationReferenceDecision ResolveCandidate(
            IReadOnlyList<GameplayTriggerArbitrationReferenceCandidate> candidates,
            int candidateIndex)
        {
            GameplayTriggerArbitrationReferenceCandidate candidate = candidates[candidateIndex];
            if (!candidate.Eligible)
            {
                return GameplayTriggerArbitrationReferenceDecision.Ineligible;
            }
            if (candidate.ExclusiveGroupId == 0)
            {
                return GameplayTriggerArbitrationReferenceDecision.Accepted;
            }
            if (!TryGetScopeKey(candidate, out ScopeKey candidateScope))
            {
                return GameplayTriggerArbitrationReferenceDecision.MissingScopeEntity;
            }

            for (int otherIndex = 0; otherIndex < candidates.Count; otherIndex++)
            {
                if (otherIndex == candidateIndex)
                {
                    continue;
                }
                GameplayTriggerArbitrationReferenceCandidate other = candidates[otherIndex];
                if (!other.Eligible ||
                    other.ExclusiveGroupId != candidate.ExclusiveGroupId ||
                    other.ArbitrationScope != candidate.ArbitrationScope ||
                    !TryGetScopeKey(other, out ScopeKey otherScope) ||
                    !candidateScope.Equals(otherScope))
                {
                    continue;
                }
                if (CompareWinner(other, candidate) < 0)
                {
                    return GameplayTriggerArbitrationReferenceDecision.Suppressed;
                }
            }
            return GameplayTriggerArbitrationReferenceDecision.Accepted;
        }

        private static bool TryGetScopeKey(
            in GameplayTriggerArbitrationReferenceCandidate candidate,
            out ScopeKey key)
        {
            GameplayEntityId subject = candidate.Occurrence.Subject;
            GameplayEntityId instigator = candidate.Occurrence.Instigator;
            switch (candidate.ArbitrationScope)
            {
                case GameplayTriggerArbitrationScope.Global:
                    key = default;
                    return true;
                case GameplayTriggerArbitrationScope.PerSubject:
                    if (!subject.IsValid)
                    {
                        key = default;
                        return false;
                    }
                    key = new ScopeKey(subject, default);
                    return true;
                case GameplayTriggerArbitrationScope.PerInstigator:
                    if (!instigator.IsValid)
                    {
                        key = default;
                        return false;
                    }
                    key = new ScopeKey(default, instigator);
                    return true;
                case GameplayTriggerArbitrationScope.PerPair:
                    if (!subject.IsValid || !instigator.IsValid)
                    {
                        key = default;
                        return false;
                    }
                    key = new ScopeKey(subject, instigator);
                    return true;
                default:
                    key = default;
                    return false;
            }
        }

        private static int CompareWinner(
            GameplayTriggerArbitrationReferenceCandidate left,
            GameplayTriggerArbitrationReferenceCandidate right)
        {
            int priority = right.WinnerPriority.CompareTo(left.WinnerPriority);
            return priority != 0 ? priority : CompareIdentity(left, right);
        }

        private static int CompareExecution(
            GameplayTriggerArbitrationReferenceCandidate left,
            GameplayTriggerArbitrationReferenceCandidate right)
        {
            int execution = left.ExecutionOrder.CompareTo(right.ExecutionOrder);
            return execution != 0 ? execution : CompareIdentity(left, right);
        }

        private static int CompareResultIdentity(
            GameplayTriggerArbitrationReferenceResult left,
            GameplayTriggerArbitrationReferenceResult right) =>
            CompareIdentity(left.Candidate, right.Candidate);

        private static int CompareIdentity(
            GameplayTriggerArbitrationReferenceCandidate left,
            GameplayTriggerArbitrationReferenceCandidate right)
        {
            int result = left.DefinitionId.CompareTo(right.DefinitionId);
            if (result != 0)
            {
                return result;
            }
            result = left.RouteId.CompareTo(right.RouteId);
            return result != 0
                ? result
                : GameplayTriggerReferenceOccurrence.Compare(left.Occurrence, right.Occurrence);
        }

        private readonly struct ScopeKey : IEquatable<ScopeKey>
        {
            public ScopeKey(GameplayEntityId subject, GameplayEntityId instigator)
            {
                Subject = subject;
                Instigator = instigator;
            }

            private GameplayEntityId Subject { get; }
            private GameplayEntityId Instigator { get; }

            public bool Equals(ScopeKey other) =>
                Subject == other.Subject && Instigator == other.Instigator;

            public override bool Equals(object obj) => obj is ScopeKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    return (Subject.GetHashCode() * 397) ^ Instigator.GetHashCode();
                }
            }
        }
    }
}

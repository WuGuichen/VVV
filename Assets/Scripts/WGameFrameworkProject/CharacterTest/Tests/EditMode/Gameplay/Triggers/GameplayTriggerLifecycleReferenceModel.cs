using System;
using System.Collections.Generic;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers;

namespace MxFramework.Tests.Gameplay.Triggers
{
    internal readonly struct GameplayTriggerLifecycleReferenceOccurrence
    {
        public GameplayTriggerLifecycleReferenceOccurrence(
            long frame,
            GameplayTriggerOccurrenceKind kind,
            GameplayEntityId subject,
            long presenceEpoch,
            long presenceStartFrame)
        {
            Frame = frame;
            Kind = kind;
            Subject = subject;
            PresenceEpoch = presenceEpoch;
            PresenceStartFrame = presenceStartFrame;
        }

        public long Frame { get; }
        public GameplayTriggerOccurrenceKind Kind { get; }
        public GameplayEntityId Subject { get; }
        public long PresenceEpoch { get; }
        public long PresenceStartFrame { get; }
    }

    internal readonly struct GameplayTriggerLifecycleReferenceActivation
    {
        public GameplayTriggerLifecycleReferenceActivation(
            long activationId,
            GameplayEntityId subject)
        {
            ActivationId = activationId;
            Subject = subject;
        }

        public long ActivationId { get; }
        public GameplayEntityId Subject { get; }
    }

    internal readonly struct GameplayTriggerLifecycleReferenceRelation
    {
        public GameplayTriggerLifecycleReferenceRelation(
            GameplayEntityId subject,
            bool active,
            long presenceEpoch,
            long presenceStartFrame)
        {
            Subject = subject;
            Active = active;
            PresenceEpoch = presenceEpoch;
            PresenceStartFrame = presenceStartFrame;
        }

        public GameplayEntityId Subject { get; }
        public bool Active { get; }
        public long PresenceEpoch { get; }
        public long PresenceStartFrame { get; }
    }

    internal readonly struct GameplayTriggerLifecycleReferenceRouteState
    {
        public GameplayTriggerLifecycleReferenceRouteState(
            GameplayEntityId subject,
            int activationCount,
            long lastActivationFrame,
            long lastActivatedPresenceEpoch)
        {
            Subject = subject;
            ActivationCount = activationCount;
            LastActivationFrame = lastActivationFrame;
            LastActivatedPresenceEpoch = lastActivatedPresenceEpoch;
        }

        public GameplayEntityId Subject { get; }
        public int ActivationCount { get; }
        public long LastActivationFrame { get; }
        public long LastActivatedPresenceEpoch { get; }
    }

    /// <summary>
    /// Test-only lifecycle oracle. It owns list/dictionary state and performs canonicalization
    /// independently from the production fixed relation and route-state tables.
    /// </summary>
    internal sealed class GameplayTriggerLifecycleReferenceModel
    {
        private readonly int _maxActivations;
        private readonly bool _oncePerPresence;
        private readonly List<Relation> _relations = new List<Relation>();
        private readonly Dictionary<GameplayEntityId, RouteState> _routeStates =
            new Dictionary<GameplayEntityId, RouteState>();
        private readonly List<GameplayEntityId> _normalized = new List<GameplayEntityId>();
        private readonly List<GameplayEntityId> _pendingSubjects =
            new List<GameplayEntityId>();
        private readonly List<GameplayTriggerLifecycleReferenceOccurrence> _occurrences =
            new List<GameplayTriggerLifecycleReferenceOccurrence>();
        private readonly List<GameplayTriggerLifecycleReferenceActivation> _activations =
            new List<GameplayTriggerLifecycleReferenceActivation>();
        private readonly List<GameplayTriggerLifecycleReferenceRelation> _relationProjection =
            new List<GameplayTriggerLifecycleReferenceRelation>();
        private readonly List<GameplayTriggerLifecycleReferenceRouteState> _routeProjection =
            new List<GameplayTriggerLifecycleReferenceRouteState>();
        private long _nextActivationId = 1L;
        private int _activeCount;
        private long _aggregatePresenceEpoch;
        private bool _hasPendingInput;

        public GameplayTriggerLifecycleReferenceModel(
            bool initiallyEnabled = true,
            int maxActivations = 0,
            bool oncePerPresence = false)
        {
            Enabled = initiallyEnabled;
            _maxActivations = maxActivations;
            _oncePerPresence = oncePerPresence;
        }

        public bool Enabled { get; private set; }
        public int ActiveCount => _activeCount;
        public long AggregatePresenceEpoch => _aggregatePresenceEpoch;
        public long NextActivationId => _nextActivationId;
        public IReadOnlyList<GameplayTriggerLifecycleReferenceOccurrence> Occurrences =>
            _occurrences;
        public IReadOnlyList<GameplayTriggerLifecycleReferenceActivation> Activations =>
            _activations;
        public IReadOnlyList<GameplayTriggerLifecycleReferenceRelation> Relations =>
            _relationProjection;
        public IReadOnlyList<GameplayTriggerLifecycleReferenceRouteState> RouteStates =>
            _routeProjection;

        public void SetEnabled(bool enabled)
        {
            if (Enabled == enabled)
            {
                return;
            }

            Enabled = enabled;
            if (!enabled)
            {
                ClearPendingInput();
                ClearSourceState();
            }
            ClearFrameOutputs();
            CaptureState();
        }

        public void Reset()
        {
            ClearPendingInput();
            ClearSourceState();
            _routeStates.Clear();
            ClearFrameOutputs();
            CaptureState();
        }

        public bool EvaluateOccupancy(long frame, IReadOnlyList<GameplayEntityId> subjects)
        {
            if (!SubmitOccupancy(subjects))
            {
                return false;
            }

            return EvaluatePending(frame);
        }

        public bool SubmitOccupancy(IReadOnlyList<GameplayEntityId> subjects)
        {
            if (!Enabled || _hasPendingInput)
            {
                return false;
            }

            _pendingSubjects.Clear();
            for (int i = 0; i < subjects.Count; i++)
            {
                _pendingSubjects.Add(subjects[i]);
            }
            _hasPendingInput = true;
            return true;
        }

        public bool EvaluatePending(long frame)
        {
            if (!_hasPendingInput)
            {
                ClearFrameOutputs();
                CaptureState();
                return false;
            }

            Normalize(_pendingSubjects);
            ClearPendingInput();
            ClearFrameOutputs();
            int activeBefore = _activeCount;

            for (int i = 0; i < _normalized.Count; i++)
            {
                GameplayEntityId subject = _normalized[i];
                int relationIndex = FindRelation(subject);
                if (relationIndex < 0)
                {
                    relationIndex = _relations.Count;
                    _relations.Add(new Relation(subject));
                }

                Relation relation = _relations[relationIndex];
                GameplayTriggerOccurrenceKind kind;
                if (!relation.Active)
                {
                    relation.Active = true;
                    relation.PresenceEpoch++;
                    relation.PresenceStartFrame = frame;
                    kind = GameplayTriggerOccurrenceKind.Entered;
                }
                else
                {
                    kind = GameplayTriggerOccurrenceKind.Stayed;
                }
                relation.Seen = true;
                _relations[relationIndex] = relation;
                _occurrences.Add(new GameplayTriggerLifecycleReferenceOccurrence(
                    frame,
                    kind,
                    subject,
                    relation.PresenceEpoch,
                    relation.PresenceStartFrame));
            }

            for (int i = 0; i < _relations.Count; i++)
            {
                Relation relation = _relations[i];
                if (relation.Active && !relation.Seen)
                {
                    relation.Active = false;
                    _occurrences.Add(new GameplayTriggerLifecycleReferenceOccurrence(
                        frame,
                        GameplayTriggerOccurrenceKind.Exited,
                        relation.Subject,
                        relation.PresenceEpoch,
                        relation.PresenceStartFrame));
                }
                relation.Seen = false;
                _relations[i] = relation;
            }

            _occurrences.Sort(CompareOccurrences);
            RefreshActiveCount(activeBefore);
            ActivateEntered(frame);
            CaptureState();
            return true;
        }

        private void ClearPendingInput()
        {
            _pendingSubjects.Clear();
            _hasPendingInput = false;
        }

        private void ActivateEntered(long frame)
        {
            for (int i = 0; i < _occurrences.Count; i++)
            {
                GameplayTriggerLifecycleReferenceOccurrence occurrence = _occurrences[i];
                if (occurrence.Kind != GameplayTriggerOccurrenceKind.Entered)
                {
                    continue;
                }

                _routeStates.TryGetValue(occurrence.Subject, out RouteState state);
                if (_maxActivations > 0 && state.ActivationCount >= _maxActivations)
                {
                    continue;
                }
                if (_oncePerPresence &&
                    state.LastActivatedPresenceEpoch == occurrence.PresenceEpoch)
                {
                    continue;
                }

                state.ActivationCount++;
                state.LastActivationFrame = frame;
                if (_oncePerPresence)
                {
                    state.LastActivatedPresenceEpoch = occurrence.PresenceEpoch;
                }
                _routeStates[occurrence.Subject] = state;
                _activations.Add(new GameplayTriggerLifecycleReferenceActivation(
                    _nextActivationId++,
                    occurrence.Subject));
            }
        }

        private void Normalize(IReadOnlyList<GameplayEntityId> subjects)
        {
            _normalized.Clear();
            for (int i = 0; i < subjects.Count; i++)
            {
                GameplayEntityId subject = subjects[i];
                bool duplicate = false;
                for (int j = 0; j < _normalized.Count; j++)
                {
                    if (_normalized[j] == subject)
                    {
                        duplicate = true;
                        break;
                    }
                }
                if (!duplicate)
                {
                    _normalized.Add(subject);
                }
            }
            _normalized.Sort((left, right) => left.CompareTo(right));
        }

        private void ClearSourceState()
        {
            for (int i = 0; i < _relations.Count; i++)
            {
                Relation relation = _relations[i];
                relation.Active = false;
                relation.Seen = false;
                _relations[i] = relation;
            }
            _activeCount = 0;
        }

        private void RefreshActiveCount(int activeBefore)
        {
            int activeAfter = 0;
            for (int i = 0; i < _relations.Count; i++)
            {
                if (_relations[i].Active)
                {
                    activeAfter++;
                }
            }
            if (activeBefore == 0 && activeAfter > 0)
            {
                _aggregatePresenceEpoch++;
            }
            _activeCount = activeAfter;
        }

        private void CaptureState()
        {
            _relationProjection.Clear();
            for (int i = 0; i < _relations.Count; i++)
            {
                Relation relation = _relations[i];
                _relationProjection.Add(new GameplayTriggerLifecycleReferenceRelation(
                    relation.Subject,
                    relation.Active,
                    relation.PresenceEpoch,
                    relation.PresenceStartFrame));
            }
            _relationProjection.Sort((left, right) => left.Subject.CompareTo(right.Subject));

            _routeProjection.Clear();
            foreach (KeyValuePair<GameplayEntityId, RouteState> pair in _routeStates)
            {
                _routeProjection.Add(new GameplayTriggerLifecycleReferenceRouteState(
                    pair.Key,
                    pair.Value.ActivationCount,
                    pair.Value.LastActivationFrame,
                    pair.Value.LastActivatedPresenceEpoch));
            }
            _routeProjection.Sort((left, right) => left.Subject.CompareTo(right.Subject));
        }

        private void ClearFrameOutputs()
        {
            _occurrences.Clear();
            _activations.Clear();
        }

        private int FindRelation(GameplayEntityId subject)
        {
            for (int i = 0; i < _relations.Count; i++)
            {
                if (_relations[i].Subject == subject)
                {
                    return i;
                }
            }
            return -1;
        }

        private static int CompareOccurrences(
            GameplayTriggerLifecycleReferenceOccurrence left,
            GameplayTriggerLifecycleReferenceOccurrence right)
        {
            int result = left.Subject.CompareTo(right.Subject);
            return result != 0 ? result : left.Kind.CompareTo(right.Kind);
        }

        private struct Relation
        {
            public Relation(GameplayEntityId subject)
            {
                Subject = subject;
                Active = false;
                Seen = false;
                PresenceEpoch = 0L;
                PresenceStartFrame = 0L;
            }

            public GameplayEntityId Subject;
            public bool Active;
            public bool Seen;
            public long PresenceEpoch;
            public long PresenceStartFrame;
        }

        private struct RouteState
        {
            public int ActivationCount;
            public long LastActivationFrame;
            public long LastActivatedPresenceEpoch;
        }
    }
}

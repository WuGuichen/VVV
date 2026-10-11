using System;
using System.Collections.Generic;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers;

namespace MxFramework.Tests.Gameplay.Triggers
{
    /// <summary>
    /// Test-only readable oracle. It intentionally uses BCL collections and sorting instead of
    /// production Trigger runtime storage, normalization, reducer, policy or arbitration helpers.
    /// </summary>
    internal sealed class GameplayTriggerReferenceModel
    {
        private readonly GameplayTriggerInstanceId _instanceId;
        private readonly Dictionary<GameplayEntityId, PresenceState> _occupancy = new Dictionary<GameplayEntityId, PresenceState>();
        private readonly Dictionary<GameplayEntityId, ThresholdState> _thresholds = new Dictionary<GameplayEntityId, ThresholdState>();
        private readonly List<GameplayEntityId> _currentSubjects = new List<GameplayEntityId>();
        private readonly List<GameplayEntityId> _departedSubjects = new List<GameplayEntityId>();
        private readonly List<GameplayTriggerReferenceOccurrence> _occurrences = new List<GameplayTriggerReferenceOccurrence>();
        private readonly List<GameplayTriggerSignal> _signals = new List<GameplayTriggerSignal>();
        private int _occupancyActiveCount;
        private long _occupancyAggregatePresenceEpoch;

        public GameplayTriggerReferenceModel(GameplayTriggerInstanceId instanceId)
        {
            if (!instanceId.IsValid) throw new ArgumentOutOfRangeException(nameof(instanceId));
            _instanceId = instanceId;
        }

        public IReadOnlyList<GameplayTriggerReferenceOccurrence> Occurrences => _occurrences;
        public int OccupancyActiveCount => _occupancyActiveCount;
        public long OccupancyAggregatePresenceEpoch => _occupancyAggregatePresenceEpoch;

        public void ReduceOccupancy(long frame, IReadOnlyList<GameplayEntityId> submittedSubjects)
        {
            _occurrences.Clear();
            _currentSubjects.Clear();
            _departedSubjects.Clear();

            for (int i = 0; i < submittedSubjects.Count; i++)
            {
                GameplayEntityId subject = submittedSubjects[i];
                if (!subject.IsValid) throw new ArgumentException("Reference occupancy input contains an invalid subject.", nameof(submittedSubjects));
                if (!_currentSubjects.Contains(subject)) _currentSubjects.Add(subject);
            }
            _currentSubjects.Sort();

            foreach (KeyValuePair<GameplayEntityId, PresenceState> pair in _occupancy)
            {
                if (pair.Value.Active && !_currentSubjects.Contains(pair.Key)) _departedSubjects.Add(pair.Key);
            }
            _departedSubjects.Sort();

            for (int i = 0; i < _currentSubjects.Count; i++)
            {
                GameplayEntityId subject = _currentSubjects[i];
                _occupancy.TryGetValue(subject, out PresenceState state);
                GameplayTriggerOccurrenceKind kind;
                if (!state.Active)
                {
                    state.Active = true;
                    state.PresenceEpoch++;
                    state.PresenceStartFrame = frame;
                    kind = GameplayTriggerOccurrenceKind.Entered;
                }
                else
                {
                    kind = GameplayTriggerOccurrenceKind.Stayed;
                }
                _occupancy[subject] = state;
                _occurrences.Add(GameplayTriggerReferenceOccurrence.Occupancy(
                    frame,
                    _instanceId,
                    kind,
                    subject,
                    state.PresenceEpoch,
                    state.PresenceStartFrame));
            }

            for (int i = 0; i < _departedSubjects.Count; i++)
            {
                GameplayEntityId subject = _departedSubjects[i];
                PresenceState state = _occupancy[subject];
                state.Active = false;
                _occupancy[subject] = state;
                _occurrences.Add(GameplayTriggerReferenceOccurrence.Occupancy(
                    frame,
                    _instanceId,
                    GameplayTriggerOccurrenceKind.Exited,
                    subject,
                    state.PresenceEpoch,
                    state.PresenceStartFrame));
            }
            int activeBefore = _occupancyActiveCount;
            _occupancyActiveCount = _currentSubjects.Count;
            if (activeBefore == 0 && _occupancyActiveCount > 0)
            {
                _occupancyAggregatePresenceEpoch++;
            }
            CanonicalizeOccurrences();
        }

        public void CaptureOccupancyRelations(
            List<GameplayTriggerReferenceRelationState> output)
        {
            output.Clear();
            foreach (KeyValuePair<GameplayEntityId, PresenceState> pair in _occupancy)
            {
                output.Add(new GameplayTriggerReferenceRelationState(
                    _instanceId,
                    pair.Key,
                    pair.Value.Active,
                    primed: false,
                    scalar: default,
                    pair.Value.PresenceEpoch,
                    pair.Value.PresenceStartFrame));
            }
            output.Sort(GameplayTriggerReferenceRelationState.Compare);
        }

        public void ReduceThreshold(
            long frame,
            IReadOnlyList<GameplayTriggerScalarSample> samples,
            GameplayTriggerThresholdDirection direction,
            Fix64 enterThreshold,
            Fix64 exitThreshold,
            GameplayTriggerInitialSamplePolicy initialPolicy,
            GameplayTriggerSubjectDeparturePolicy departurePolicy)
        {
            _occurrences.Clear();
            _currentSubjects.Clear();
            var sorted = new List<GameplayTriggerScalarSample>(samples.Count);
            for (int i = 0; i < samples.Count; i++) sorted.Add(samples[i]);
            sorted.Sort((left, right) => left.Subject.CompareTo(right.Subject));
            for (int i = 1; i < sorted.Count; i++)
            {
                if (sorted[i - 1].Subject == sorted[i].Subject)
                    throw new ArgumentException("Reference scalar input contains duplicate subjects.", nameof(samples));
            }

            for (int i = 0; i < sorted.Count; i++)
            {
                GameplayTriggerScalarSample sample = sorted[i];
                _currentSubjects.Add(sample.Subject);
                _thresholds.TryGetValue(sample.Subject, out ThresholdState state);
                Fix64 previous = state.Scalar;
                bool wasPrimed = state.Primed;
                bool wasActive = state.Active;
                bool active = wasActive
                    ? RemainsActive(direction, exitThreshold, sample.Value)
                    : Enters(direction, enterThreshold, sample.Value);
                state.Primed = true;
                state.Scalar = sample.Value;

                if (!wasPrimed)
                {
                    state.Active = active;
                    if (active)
                    {
                        state.PresenceEpoch++;
                        state.PresenceStartFrame = frame;
                        if (initialPolicy == GameplayTriggerInitialSamplePolicy.EmitEnteredIfActive)
                            AddThreshold(frame, GameplayTriggerOccurrenceKind.Entered, sample.Subject, previous, sample.Value, state);
                    }
                }
                else if (!wasActive && active)
                {
                    state.Active = true;
                    state.PresenceEpoch++;
                    state.PresenceStartFrame = frame;
                    AddThreshold(frame, GameplayTriggerOccurrenceKind.Entered, sample.Subject, previous, sample.Value, state);
                }
                else if (wasActive && !active)
                {
                    state.Active = false;
                    AddThreshold(frame, GameplayTriggerOccurrenceKind.Exited, sample.Subject, previous, sample.Value, state);
                }
                else if (active)
                {
                    AddThreshold(frame, GameplayTriggerOccurrenceKind.Stayed, sample.Subject, previous, sample.Value, state);
                }
                _thresholds[sample.Subject] = state;
            }

            _departedSubjects.Clear();
            foreach (KeyValuePair<GameplayEntityId, ThresholdState> pair in _thresholds)
            {
                if (pair.Value.Primed && !_currentSubjects.Contains(pair.Key)) _departedSubjects.Add(pair.Key);
            }
            _departedSubjects.Sort();
            for (int i = 0; i < _departedSubjects.Count; i++)
            {
                GameplayEntityId subject = _departedSubjects[i];
                ThresholdState state = _thresholds[subject];
                if (state.Active && departurePolicy == GameplayTriggerSubjectDeparturePolicy.EmitExited)
                    AddThreshold(frame, GameplayTriggerOccurrenceKind.Exited, subject, state.Scalar, state.Scalar, state);
                state.Active = false;
                state.Primed = false;
                _thresholds[subject] = state;
            }
            CanonicalizeOccurrences();
        }

        public void ReduceUnorderedSignals(long frame, IReadOnlyList<GameplayTriggerSignal> submittedSignals)
        {
            _occurrences.Clear();
            _signals.Clear();
            for (int i = 0; i < submittedSignals.Count; i++) _signals.Add(submittedSignals[i]);
            _signals.Sort(CompareSignals);
            for (int i = 0; i < _signals.Count; i++)
            {
                GameplayTriggerSignal signal = _signals[i];
                _occurrences.Add(GameplayTriggerReferenceOccurrence.Signal(frame, _instanceId, signal, i));
            }
            CanonicalizeOccurrences();
        }

        private void AddThreshold(
            long frame,
            GameplayTriggerOccurrenceKind kind,
            GameplayEntityId subject,
            Fix64 previous,
            Fix64 current,
            ThresholdState state)
        {
            _occurrences.Add(GameplayTriggerReferenceOccurrence.Threshold(
                frame,
                instanceId: _instanceId,
                kind,
                subject,
                previous,
                current,
                state.PresenceEpoch,
                state.PresenceStartFrame));
        }

        private void CanonicalizeOccurrences()
        {
            _occurrences.Sort(GameplayTriggerReferenceOccurrence.Compare);
            for (int i = 0; i < _occurrences.Count; i++)
                _occurrences[i] = _occurrences[i].WithOrderingValue(i);
        }

        private static bool Enters(GameplayTriggerThresholdDirection direction, Fix64 threshold, Fix64 value) =>
            direction == GameplayTriggerThresholdDirection.High ? value >= threshold : value <= threshold;

        private static bool RemainsActive(GameplayTriggerThresholdDirection direction, Fix64 threshold, Fix64 value) =>
            direction == GameplayTriggerThresholdDirection.High ? value >= threshold : value <= threshold;

        private static int CompareSignals(GameplayTriggerSignal left, GameplayTriggerSignal right)
        {
            int result = left.SignalId.CompareTo(right.SignalId);
            if (result != 0) return result;
            result = left.Subject.CompareTo(right.Subject);
            if (result != 0) return result;
            result = left.Instigator.CompareTo(right.Instigator);
            if (result != 0) return result;
            result = left.Payload0.CompareTo(right.Payload0);
            if (result != 0) return result;
            result = left.Payload1.CompareTo(right.Payload1);
            return result != 0 ? result : left.Payload2.CompareTo(right.Payload2);
        }

        private struct PresenceState
        {
            public bool Active;
            public long PresenceEpoch;
            public long PresenceStartFrame;
        }

        private struct ThresholdState
        {
            public bool Primed;
            public bool Active;
            public Fix64 Scalar;
            public long PresenceEpoch;
            public long PresenceStartFrame;
        }
    }

    internal readonly struct GameplayTriggerReferenceRelationState
    {
        public GameplayTriggerReferenceRelationState(
            GameplayTriggerInstanceId instanceId,
            GameplayEntityId subject,
            bool active,
            bool primed,
            Fix64 scalar,
            long presenceEpoch,
            long presenceStartFrame)
        {
            InstanceId = instanceId;
            Subject = subject;
            Active = active;
            Primed = primed;
            Scalar = scalar;
            PresenceEpoch = presenceEpoch;
            PresenceStartFrame = presenceStartFrame;
        }

        public GameplayTriggerInstanceId InstanceId { get; }
        public GameplayEntityId Subject { get; }
        public bool Active { get; }
        public bool Primed { get; }
        public Fix64 Scalar { get; }
        public long PresenceEpoch { get; }
        public long PresenceStartFrame { get; }

        public static int Compare(
            GameplayTriggerReferenceRelationState left,
            GameplayTriggerReferenceRelationState right)
        {
            int result = left.InstanceId.CompareTo(right.InstanceId);
            return result != 0 ? result : left.Subject.CompareTo(right.Subject);
        }
    }

    internal readonly struct GameplayTriggerReferenceOccurrence
    {
        private GameplayTriggerReferenceOccurrence(
            long frame,
            GameplayTriggerInstanceId instanceId,
            GameplayTriggerOccurrenceKind kind,
            GameplayEntityId subject,
            GameplayEntityId instigator,
            int signalId,
            Fix64 previousScalar,
            Fix64 currentScalar,
            long payload0,
            long payload1,
            long payload2,
            long orderingValue,
            long presenceEpoch,
            long presenceStartFrame)
        {
            Frame = frame;
            InstanceId = instanceId;
            Kind = kind;
            Subject = subject;
            Instigator = instigator;
            SignalId = signalId;
            PreviousScalar = previousScalar;
            CurrentScalar = currentScalar;
            Payload0 = payload0;
            Payload1 = payload1;
            Payload2 = payload2;
            OrderingValue = orderingValue;
            PresenceEpoch = presenceEpoch;
            PresenceStartFrame = presenceStartFrame;
        }

        public long Frame { get; }
        public GameplayTriggerInstanceId InstanceId { get; }
        public GameplayTriggerOccurrenceKind Kind { get; }
        public GameplayEntityId Subject { get; }
        public GameplayEntityId Instigator { get; }
        public int SignalId { get; }
        public Fix64 PreviousScalar { get; }
        public Fix64 CurrentScalar { get; }
        public long Payload0 { get; }
        public long Payload1 { get; }
        public long Payload2 { get; }
        public long OrderingValue { get; }
        public long PresenceEpoch { get; }
        public long PresenceStartFrame { get; }

        public static GameplayTriggerReferenceOccurrence Occupancy(
            long frame,
            GameplayTriggerInstanceId instanceId,
            GameplayTriggerOccurrenceKind kind,
            GameplayEntityId subject,
            long presenceEpoch,
            long presenceStartFrame) =>
            new GameplayTriggerReferenceOccurrence(
                frame, instanceId, kind, subject, default, 0, default, default, 0L, 0L, 0L, 0L, presenceEpoch, presenceStartFrame);

        public static GameplayTriggerReferenceOccurrence Threshold(
            long frame,
            GameplayTriggerInstanceId instanceId,
            GameplayTriggerOccurrenceKind kind,
            GameplayEntityId subject,
            Fix64 previous,
            Fix64 current,
            long presenceEpoch,
            long presenceStartFrame) =>
            new GameplayTriggerReferenceOccurrence(
                frame, instanceId, kind, subject, default, 0, previous, current, 0L, 0L, 0L, 0L, presenceEpoch, presenceStartFrame);

        public static GameplayTriggerReferenceOccurrence Signal(
            long frame,
            GameplayTriggerInstanceId instanceId,
            GameplayTriggerSignal signal,
            long orderingValue) =>
            new GameplayTriggerReferenceOccurrence(
                frame,
                instanceId,
                GameplayTriggerOccurrenceKind.Pulsed,
                signal.Subject,
                signal.Instigator,
                signal.SignalId,
                default,
                default,
                signal.Payload0,
                signal.Payload1,
                signal.Payload2,
                orderingValue,
                0L,
                0L);

        public GameplayTriggerReferenceOccurrence WithOrderingValue(long orderingValue) =>
            new GameplayTriggerReferenceOccurrence(
                Frame,
                InstanceId,
                Kind,
                Subject,
                Instigator,
                SignalId,
                PreviousScalar,
                CurrentScalar,
                Payload0,
                Payload1,
                Payload2,
                orderingValue,
                PresenceEpoch,
                PresenceStartFrame);

        public static int Compare(GameplayTriggerReferenceOccurrence left, GameplayTriggerReferenceOccurrence right)
        {
            int result = left.InstanceId.CompareTo(right.InstanceId);
            if (result != 0) return result;
            result = left.Subject.CompareTo(right.Subject);
            if (result != 0) return result;
            result = left.Instigator.CompareTo(right.Instigator);
            if (result != 0) return result;
            result = left.Kind.CompareTo(right.Kind);
            if (result != 0) return result;
            result = left.SignalId.CompareTo(right.SignalId);
            if (result != 0) return result;
            if (left.Kind == GameplayTriggerOccurrenceKind.Pulsed)
            {
                result = left.OrderingValue.CompareTo(right.OrderingValue);
                if (result != 0) return result;
            }
            result = left.CurrentScalar.RawValue.CompareTo(right.CurrentScalar.RawValue);
            if (result != 0) return result;
            result = left.Payload0.CompareTo(right.Payload0);
            if (result != 0) return result;
            result = left.Payload1.CompareTo(right.Payload1);
            if (result != 0) return result;
            result = left.Payload2.CompareTo(right.Payload2);
            return result != 0 ? result : left.OrderingValue.CompareTo(right.OrderingValue);
        }
    }
}

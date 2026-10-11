using System;
using System.Collections.Generic;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers;

namespace MxFramework.Tests.Gameplay.Triggers
{
    internal enum GameplayTriggerPolicyReferenceDecision
    {
        Accepted = 0,
        MissingScopeEntity = 1,
        CountExceeded = 2,
        Cooldown = 3,
        StayInterval = 4,
        PresenceLimit = 5
    }

    internal readonly struct GameplayTriggerPolicyReferenceOccurrence
    {
        public GameplayTriggerPolicyReferenceOccurrence(
            long frame,
            GameplayTriggerInstanceId instanceId,
            GameplayTriggerOccurrenceKind kind,
            GameplayEntityId subject = default,
            GameplayEntityId instigator = default,
            long presenceEpoch = 0L,
            long presenceStartFrame = 0L,
            long aggregatePresenceEpoch = 0L)
        {
            Frame = frame;
            InstanceId = instanceId;
            Kind = kind;
            Subject = subject;
            Instigator = instigator;
            PresenceEpoch = presenceEpoch;
            PresenceStartFrame = presenceStartFrame;
            AggregatePresenceEpoch = aggregatePresenceEpoch;
        }

        public long Frame { get; }
        public GameplayTriggerInstanceId InstanceId { get; }
        public GameplayTriggerOccurrenceKind Kind { get; }
        public GameplayEntityId Subject { get; }
        public GameplayEntityId Instigator { get; }
        public long PresenceEpoch { get; }
        public long PresenceStartFrame { get; }
        public long AggregatePresenceEpoch { get; }
    }

    /// <summary>
    /// Test-only policy oracle. This intentionally uses a dictionary and separate check/commit
    /// operations instead of the production runtime's fixed route-state table.
    /// </summary>
    internal sealed class GameplayTriggerPolicyReferenceModel
    {
        private readonly Dictionary<StateKey, State> _states = new Dictionary<StateKey, State>();

        public GameplayTriggerPolicyReferenceDecision Check(
            GameplayTriggerRouteId routeId,
            in GameplayTriggerActivationPolicy policy,
            in GameplayTriggerPolicyReferenceOccurrence occurrence)
        {
            if (!TryCreateKey(routeId, policy.StateScope, occurrence, out StateKey key))
            {
                return GameplayTriggerPolicyReferenceDecision.MissingScopeEntity;
            }

            _states.TryGetValue(key, out State state);
            if (policy.MaxActivations > 0 && state.ActivationCount >= policy.MaxActivations)
            {
                return GameplayTriggerPolicyReferenceDecision.CountExceeded;
            }
            if (state.HasActivated && occurrence.Frame < SaturatingAdd(state.LastActivationFrame, policy.CooldownFrames))
            {
                return GameplayTriggerPolicyReferenceDecision.Cooldown;
            }
            if (occurrence.Kind == GameplayTriggerOccurrenceKind.Stayed)
            {
                long nextStayFrame = state.HasStaySchedule
                    ? state.NextStayFrame
                    : SaturatingAdd(occurrence.PresenceStartFrame, policy.StayIntervalFrames);
                if (occurrence.Frame < nextStayFrame)
                {
                    return GameplayTriggerPolicyReferenceDecision.StayInterval;
                }
            }
            if (policy.PresenceLimit == GameplayTriggerPresenceLimit.OncePerPresence)
            {
                long epoch = policy.StateScope == GameplayTriggerStateScope.PerInstance
                    ? occurrence.AggregatePresenceEpoch
                    : occurrence.PresenceEpoch;
                if (epoch <= 0L || state.LastActivatedPresenceEpoch == epoch)
                {
                    return GameplayTriggerPolicyReferenceDecision.PresenceLimit;
                }
            }
            return GameplayTriggerPolicyReferenceDecision.Accepted;
        }

        public void Commit(
            GameplayTriggerRouteId routeId,
            in GameplayTriggerActivationPolicy policy,
            in GameplayTriggerPolicyReferenceOccurrence occurrence)
        {
            if (Check(routeId, policy, occurrence) != GameplayTriggerPolicyReferenceDecision.Accepted)
            {
                throw new InvalidOperationException("Only an accepted policy occurrence can be committed.");
            }
            if (!TryCreateKey(routeId, policy.StateScope, occurrence, out StateKey key))
            {
                throw new InvalidOperationException("The accepted occurrence must have a valid policy scope key.");
            }

            _states.TryGetValue(key, out State state);
            state.ActivationCount++;
            state.HasActivated = true;
            state.LastActivationFrame = occurrence.Frame;
            if (occurrence.Kind == GameplayTriggerOccurrenceKind.Stayed)
            {
                state.HasStaySchedule = true;
                state.NextStayFrame = SaturatingAdd(occurrence.Frame, policy.StayIntervalFrames);
            }
            if (policy.PresenceLimit == GameplayTriggerPresenceLimit.OncePerPresence)
            {
                state.LastActivatedPresenceEpoch = policy.StateScope == GameplayTriggerStateScope.PerInstance
                    ? occurrence.AggregatePresenceEpoch
                    : occurrence.PresenceEpoch;
            }
            _states[key] = state;
        }

        public void CaptureStates(List<GameplayTriggerPolicyReferenceState> output)
        {
            output.Clear();
            foreach (KeyValuePair<StateKey, State> pair in _states)
            {
                output.Add(new GameplayTriggerPolicyReferenceState(
                    pair.Key.InstanceId,
                    pair.Key.RouteId,
                    pair.Key.Subject,
                    pair.Key.Instigator,
                    pair.Value.ActivationCount,
                    pair.Value.HasActivated,
                    pair.Value.LastActivationFrame,
                    pair.Value.LastActivatedPresenceEpoch,
                    pair.Value.HasStaySchedule,
                    pair.Value.NextStayFrame));
            }
            output.Sort(GameplayTriggerPolicyReferenceState.Compare);
        }

        private static bool TryCreateKey(
            GameplayTriggerRouteId routeId,
            GameplayTriggerStateScope scope,
            in GameplayTriggerPolicyReferenceOccurrence occurrence,
            out StateKey key)
        {
            GameplayEntityId subject = default;
            GameplayEntityId instigator = default;
            switch (scope)
            {
                case GameplayTriggerStateScope.PerInstance:
                    break;
                case GameplayTriggerStateScope.PerSubject:
                    if (!occurrence.Subject.IsValid)
                    {
                        key = default;
                        return false;
                    }
                    subject = occurrence.Subject;
                    break;
                case GameplayTriggerStateScope.PerInstigator:
                    if (!occurrence.Instigator.IsValid)
                    {
                        key = default;
                        return false;
                    }
                    instigator = occurrence.Instigator;
                    break;
                case GameplayTriggerStateScope.PerPair:
                    if (!occurrence.Subject.IsValid || !occurrence.Instigator.IsValid)
                    {
                        key = default;
                        return false;
                    }
                    subject = occurrence.Subject;
                    instigator = occurrence.Instigator;
                    break;
                default:
                    key = default;
                    return false;
            }

            key = new StateKey(occurrence.InstanceId, routeId, subject, instigator);
            return true;
        }

        private static long SaturatingAdd(long value, long increment)
        {
            if (increment > 0L && value > long.MaxValue - increment)
            {
                return long.MaxValue;
            }
            return value + increment;
        }

        private readonly struct StateKey : IEquatable<StateKey>
        {
            public StateKey(
                GameplayTriggerInstanceId instanceId,
                GameplayTriggerRouteId routeId,
                GameplayEntityId subject,
                GameplayEntityId instigator)
            {
                InstanceId = instanceId;
                RouteId = routeId;
                Subject = subject;
                Instigator = instigator;
            }

            public GameplayTriggerInstanceId InstanceId { get; }
            public GameplayTriggerRouteId RouteId { get; }
            public GameplayEntityId Subject { get; }
            public GameplayEntityId Instigator { get; }

            public bool Equals(StateKey other) =>
                InstanceId == other.InstanceId &&
                RouteId == other.RouteId &&
                Subject == other.Subject &&
                Instigator == other.Instigator;

            public override bool Equals(object obj) => obj is StateKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = InstanceId.GetHashCode();
                    hash = (hash * 397) ^ RouteId.GetHashCode();
                    hash = (hash * 397) ^ Subject.GetHashCode();
                    return (hash * 397) ^ Instigator.GetHashCode();
                }
            }
        }

        private struct State
        {
            public int ActivationCount;
            public bool HasActivated;
            public long LastActivationFrame;
            public long LastActivatedPresenceEpoch;
            public bool HasStaySchedule;
            public long NextStayFrame;
        }
    }

    internal readonly struct GameplayTriggerPolicyReferenceState
    {
        public GameplayTriggerPolicyReferenceState(
            GameplayTriggerInstanceId instanceId,
            GameplayTriggerRouteId routeId,
            GameplayEntityId subject,
            GameplayEntityId instigator,
            int activationCount,
            bool hasActivated,
            long lastActivationFrame,
            long lastActivatedPresenceEpoch,
            bool hasStaySchedule,
            long nextStayFrame)
        {
            InstanceId = instanceId;
            RouteId = routeId;
            Subject = subject;
            Instigator = instigator;
            ActivationCount = activationCount;
            HasActivated = hasActivated;
            LastActivationFrame = lastActivationFrame;
            LastActivatedPresenceEpoch = lastActivatedPresenceEpoch;
            HasStaySchedule = hasStaySchedule;
            NextStayFrame = nextStayFrame;
        }

        public GameplayTriggerInstanceId InstanceId { get; }
        public GameplayTriggerRouteId RouteId { get; }
        public GameplayEntityId Subject { get; }
        public GameplayEntityId Instigator { get; }
        public int ActivationCount { get; }
        public bool HasActivated { get; }
        public long LastActivationFrame { get; }
        public long LastActivatedPresenceEpoch { get; }
        public bool HasStaySchedule { get; }
        public long NextStayFrame { get; }

        public static int Compare(
            GameplayTriggerPolicyReferenceState left,
            GameplayTriggerPolicyReferenceState right)
        {
            int result = left.InstanceId.CompareTo(right.InstanceId);
            if (result != 0) return result;
            result = left.RouteId.CompareTo(right.RouteId);
            if (result != 0) return result;
            result = left.Subject.CompareTo(right.Subject);
            return result != 0 ? result : left.Instigator.CompareTo(right.Instigator);
        }
    }
}

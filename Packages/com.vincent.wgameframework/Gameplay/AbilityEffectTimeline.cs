using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using MxFramework.Core.Math;
using MxFramework.Runtime;
using Newtonsoft.Json.Linq;

namespace MxFramework.Gameplay
{
    public enum AbilityEffectTimelineTrackKind
    {
        Combat = 0,
        Gameplay = 1,
        Presentation = 2,
    }

    public enum AbilityEffectTimelineEventKind
    {
        AreaPulse = 0,
        ShapeQueryWindow = 1,
        ApplyEffect = 100,
        SpawnProjectile = 101,
        StartEffectTimeline = 102,
        EmitGameplayEvent = 103,
        SpawnVfx = 200,
        PlayAudioCue = 201,
        CameraImpulse = 202,
        UiFeedback = 203,
    }

    public enum AbilityEffectTimelineShapeKind
    {
        None = 0,
        Sphere = 1,
        Capsule = 2,
    }

    public enum AbilityEffectTimelineAnchorMode
    {
        CapturedAnchor = 0,
        OwnerCurrent = 1,
        TriggerPoint = 2,
    }

    public enum AbilityEffectTimelineTriggerKind
    {
        Manual = 0,
        AbilityActivation = 1,
        CharacterActionEvent = 2,
        ProjectileImpact = 3,
        ProjectileExpire = 4,
        EffectTimelineEvent = 5,
    }

    public enum AbilityEffectTimelineValidationErrorCode
    {
        NullTimeline = 0,
        EmptyTimelineId = 1,
        DuplicateTimelineId = 2,
        InvalidDuration = 3,
        NullEvent = 4,
        EmptyEventId = 5,
        DuplicateEventId = 6,
        InvalidEventKind = 7,
        InvalidFrame = 8,
        InvalidEventRange = 9,
        InvalidShape = 10,
        MissingTargetTimeline = 11,
        MissingReferencedTimeline = 12,
        TimelineCycleDetected = 13,
        MaxChildDepthExceeded = 14,
    }

    public readonly struct AbilityEffectTimelineEvent
    {
        public AbilityEffectTimelineEvent(
            int eventId,
            AbilityEffectTimelineTrackKind trackKind,
            AbilityEffectTimelineEventKind eventKind,
            int frame,
            int endFrame = -1,
            AbilityEffectTimelineShapeKind shapeKind = AbilityEffectTimelineShapeKind.None,
            AbilityEffectTimelineAnchorMode anchorMode = AbilityEffectTimelineAnchorMode.CapturedAnchor,
            FixVector3 localOffset = default,
            FixVector3 localPointB = default,
            Fix64 radius = default,
            int layerMask = 0,
            int targetTimelineId = 0,
            int requestId = 0,
            int effectId = 0,
            int presentationProfileId = 0,
            bool requiresOwner = false,
            bool requiresSource = false,
            string eventLabel = "",
            string targetTimelineLabel = "",
            string requestLabel = "",
            string presentationProfileLabel = "")
        {
            if (!Enum.IsDefined(typeof(AbilityEffectTimelineTrackKind), trackKind))
                throw new ArgumentOutOfRangeException(nameof(trackKind), "Ability effect timeline track kind is not supported.");
            if (!Enum.IsDefined(typeof(AbilityEffectTimelineEventKind), eventKind))
                throw new ArgumentOutOfRangeException(nameof(eventKind), "Ability effect timeline event kind is not supported.");
            if (!Enum.IsDefined(typeof(AbilityEffectTimelineShapeKind), shapeKind))
                throw new ArgumentOutOfRangeException(nameof(shapeKind), "Ability effect timeline shape kind is not supported.");
            if (!Enum.IsDefined(typeof(AbilityEffectTimelineAnchorMode), anchorMode))
                throw new ArgumentOutOfRangeException(nameof(anchorMode), "Ability effect timeline anchor mode is not supported.");
            if (frame < 0)
                throw new ArgumentOutOfRangeException(nameof(frame), "Ability effect timeline event frame cannot be negative.");
            if (endFrame >= 0 && endFrame < frame)
                throw new ArgumentOutOfRangeException(nameof(endFrame), "Ability effect timeline event end frame cannot be before frame.");
            if (radius < Fix64.Zero)
                throw new ArgumentOutOfRangeException(nameof(radius), "Ability effect timeline event radius cannot be negative.");

            EventId = eventId;
            EventLabel = eventLabel ?? string.Empty;
            TargetTimelineLabel = targetTimelineLabel ?? string.Empty;
            RequestLabel = requestLabel ?? string.Empty;
            PresentationProfileLabel = presentationProfileLabel ?? string.Empty;
            TrackKind = trackKind;
            EventKind = eventKind;
            Frame = frame;
            EndFrame = endFrame < 0 ? frame : endFrame;
            ShapeKind = shapeKind;
            AnchorMode = anchorMode;
            LocalOffset = localOffset;
            LocalPointB = localPointB;
            Radius = radius;
            LayerMask = layerMask;
            TargetTimelineId = targetTimelineId;
            RequestId = requestId;
            EffectId = effectId < 0 ? 0 : effectId;
            PresentationProfileId = presentationProfileId;
            RequiresOwner = requiresOwner;
            RequiresSource = requiresSource;
        }

        public string EventLabel { get; }
        public string TargetTimelineLabel { get; }
        public string RequestLabel { get; }
        public string PresentationProfileLabel { get; }
        public int EventId { get; }
        public AbilityEffectTimelineTrackKind TrackKind { get; }
        public AbilityEffectTimelineEventKind EventKind { get; }
        public int Frame { get; }
        public int EndFrame { get; }
        public AbilityEffectTimelineShapeKind ShapeKind { get; }
        public AbilityEffectTimelineAnchorMode AnchorMode { get; }
        public FixVector3 LocalOffset { get; }
        public FixVector3 LocalPointB { get; }
        public Fix64 Radius { get; }
        public int LayerMask { get; }
        public int TargetTimelineId { get; }
        public int RequestId { get; }
        public int EffectId { get; }
        public int PresentationProfileId { get; }
        public bool RequiresOwner { get; }
        public bool RequiresSource { get; }
        public bool IsWindow => EndFrame > Frame;

        internal void Contribute(RuntimeContentHashAccumulator accumulator, string prefix)
        {
            accumulator.AddStringStable(prefix + ".eventId", EventLabel);
            accumulator.AddInt(prefix + ".trackKind", (int)TrackKind);
            accumulator.AddInt(prefix + ".eventKind", (int)EventKind);
            accumulator.AddInt(prefix + ".frame", Frame);
            accumulator.AddInt(prefix + ".endFrame", EndFrame);
            accumulator.AddInt(prefix + ".shapeKind", (int)ShapeKind);
            accumulator.AddInt(prefix + ".anchorMode", (int)AnchorMode);
            AddVector(accumulator, prefix + ".localOffset", LocalOffset);
            AddVector(accumulator, prefix + ".localPointB", LocalPointB);
            accumulator.AddLong(prefix + ".radius.raw", Radius.RawValue);
            accumulator.AddInt(prefix + ".layerMask", LayerMask);
            accumulator.AddStringStable(prefix + ".targetTimelineId", TargetTimelineLabel);
            accumulator.AddStringStable(prefix + ".requestId", RequestLabel);
            accumulator.AddInt(prefix + ".effectId", EffectId);
            accumulator.AddStringStable(prefix + ".presentationProfileId", PresentationProfileLabel);
            accumulator.AddInt(prefix + ".requiresOwner", RequiresOwner ? 1 : 0);
            accumulator.AddInt(prefix + ".requiresSource", RequiresSource ? 1 : 0);
        }

        private static void AddVector(RuntimeContentHashAccumulator accumulator, string prefix, FixVector3 vector)
        {
            accumulator.AddLong(prefix + ".x.raw", vector.X.RawValue);
            accumulator.AddLong(prefix + ".y.raw", vector.Y.RawValue);
            accumulator.AddLong(prefix + ".z.raw", vector.Z.RawValue);
        }
    }

    public sealed class AbilityEffectTimelineDefinition
    {
        private readonly AbilityEffectTimelineEvent[] _events;
        private readonly ReadOnlyCollection<AbilityEffectTimelineEvent> _eventsView;

        public AbilityEffectTimelineDefinition(
            int timelineId,
            int durationFrames,
            IReadOnlyList<AbilityEffectTimelineEvent> events,
            string timelineLabel = "")
        {
            TimelineLabel = timelineLabel ?? string.Empty;
            TimelineId = timelineId;
            DurationFrames = durationFrames;
            _events = CopyAndSortEvents(events);
            _eventsView = Array.AsReadOnly(_events);
        }

        public string TimelineLabel { get; }
        public int TimelineId { get; }
        public int DurationFrames { get; }
        public IReadOnlyList<AbilityEffectTimelineEvent> Events => _eventsView;

        public AbilityEffectTimelineValidationResult Validate()
        {
            return AbilityEffectTimelineValidator.Validate(this);
        }

        internal void Contribute(RuntimeContentHashAccumulator accumulator, string prefix)
        {
            accumulator.AddStringStable(prefix + ".timelineId", TimelineLabel);
            accumulator.AddInt(prefix + ".durationFrames", DurationFrames);
            var contentEvents = (AbilityEffectTimelineEvent[])_events.Clone();
            Array.Sort(contentEvents, (left, right) =>
            {
                int frame = left.Frame.CompareTo(right.Frame);
                if (frame != 0) return frame;
                int track = left.TrackKind.CompareTo(right.TrackKind);
                return track != 0 ? track : string.CompareOrdinal(left.EventLabel, right.EventLabel);
            });
            accumulator.AddInt(prefix + ".event.count", contentEvents.Length);
            for (int i = 0; i < contentEvents.Length; i++)
                contentEvents[i].Contribute(accumulator, prefix + ".event." + i);
        }

        private static AbilityEffectTimelineEvent[] CopyAndSortEvents(IReadOnlyList<AbilityEffectTimelineEvent> events)
        {
            if (events == null || events.Count == 0)
                return Array.Empty<AbilityEffectTimelineEvent>();

            var copy = new AbilityEffectTimelineEvent[events.Count];
            for (int i = 0; i < events.Count; i++)
                copy[i] = events[i];

            Array.Sort(copy, CompareEvents);
            return copy;
        }

        private static int CompareEvents(AbilityEffectTimelineEvent left, AbilityEffectTimelineEvent right)
        {
            int frame = left.Frame.CompareTo(right.Frame);
            if (frame != 0)
                return frame;
            int track = left.TrackKind.CompareTo(right.TrackKind);
            if (track != 0)
                return track;
            return left.EventId.CompareTo(right.EventId);
        }
    }

    public sealed class AbilityEffectTimelineRegistry
    {
        private readonly Dictionary<int, AbilityEffectTimelineDefinition> _byId =
            new Dictionary<int, AbilityEffectTimelineDefinition>();

        public AbilityEffectTimelineRegistry()
        {
            _contentHash = ComputeStableHash();
            _identityHash = ComputeIdentityHash();
        }

        public AbilityEffectTimelineRegistry(IEnumerable<AbilityEffectTimelineDefinition> timelines) : this()
        {
            if (timelines == null)
                return;

            foreach (AbilityEffectTimelineDefinition timeline in timelines)
            {
                if (!TryRegister(timeline, out string failure))
                    throw new ArgumentException(failure, nameof(timelines));
            }
        }

        private long _contentHash;
        private long _identityHash;
        public long PreparedIdentityHash => _identityHash;
        public long PreparedContentHash => _contentHash;
        public int Count => _byId.Count;

        public bool TryRegister(AbilityEffectTimelineDefinition timeline, out string failure)
        {
            failure = string.Empty;
            if (timeline == null)
            {
                failure = "Ability effect timeline cannot be null.";
                return false;
            }

            AbilityEffectTimelineValidationResult validation = timeline.Validate();
            if (!validation.IsValid)
            {
                failure = validation.Errors[0].Message;
                return false;
            }

            if (_byId.ContainsKey(timeline.TimelineId))
            {
                failure = "Duplicate ability effect timeline id: " + timeline.TimelineId + ".";
                return false;
            }

            _byId.Add(timeline.TimelineId, timeline);
            _contentHash = ComputeStableHash();
            _identityHash = ComputeIdentityHash();
            return true;
        }

        public bool TryGet(int timelineId, out AbilityEffectTimelineDefinition timeline)
        {
            if (timelineId <= 0)
            {
                timeline = null;
                return false;
            }

            return _byId.TryGetValue(timelineId, out timeline);
        }

        public AbilityEffectTimelineDefinition[] CreateSnapshot()
        {
            var result = new AbilityEffectTimelineDefinition[_byId.Count];
            int index = 0;
            foreach (KeyValuePair<int, AbilityEffectTimelineDefinition> pair in _byId)
                result[index++] = pair.Value;

            Array.Sort(result, (left, right) => left.TimelineId.CompareTo(right.TimelineId));
            return result;
        }

        private long ComputeIdentityHash()
        {
            var hash = new RuntimeHashAccumulator(3122, 1);
            AbilityEffectTimelineDefinition[] definitions = CreateSnapshot();
            hash.BeginArray(1, definitions.Length);
            for (int i = 0; i < definitions.Length; i++)
            {
                AbilityEffectTimelineDefinition definition = definitions[i];
                hash.BeginElement(i);
                hash.AddInt(2, definition.TimelineId);
                hash.BeginArray(3, definition.Events.Count);
                for (int e = 0; e < definition.Events.Count; e++)
                {
                    AbilityEffectTimelineEvent entry = definition.Events[e];
                    hash.BeginElement(e);
                    hash.AddInt(4, entry.EventId);
                    hash.AddInt(5, entry.TargetTimelineId);
                    hash.AddInt(6, entry.RequestId);
                    hash.AddInt(7, entry.PresentationProfileId);
                    hash.EndElement();
                }
                hash.EndArray(); hash.EndElement();
            }
            hash.EndArray();
            return hash.ToHash();
        }

        public long ComputeStableHash()
        {
            var accumulator = new RuntimeContentHashAccumulator();
            AbilityEffectTimelineDefinition[] definitions = CreateSnapshot();
            Array.Sort(definitions, (left, right) => string.CompareOrdinal(left.TimelineLabel, right.TimelineLabel));
            accumulator.AddInt("ability.effectTimeline.count", definitions.Length);
            for (int i = 0; i < definitions.Length; i++)
                definitions[i].Contribute(accumulator, "ability.effectTimeline." + i);

            return accumulator.ToHash();
        }
    }

    public readonly struct AbilityEffectTimelineValidationError
    {
        public AbilityEffectTimelineValidationError(
            AbilityEffectTimelineValidationErrorCode code,
            string message,
            int timelineId = 0,
            int eventId = 0,
            int eventIndex = -1,
            string fieldPath = "")
        {
            Code = code;
            Message = message ?? string.Empty;
            TimelineId = timelineId;
            EventId = eventId;
            EventIndex = eventIndex;
            FieldPath = fieldPath ?? string.Empty;
        }

        public AbilityEffectTimelineValidationErrorCode Code { get; }
        public string Message { get; }
        public int TimelineId { get; }
        public int EventId { get; }
        public int EventIndex { get; }
        public string FieldPath { get; }
    }

    public sealed class AbilityEffectTimelineValidationResult
    {
        private readonly AbilityEffectTimelineValidationError[] _errors;
        private readonly ReadOnlyCollection<AbilityEffectTimelineValidationError> _errorsView;

        public AbilityEffectTimelineValidationResult(IReadOnlyList<AbilityEffectTimelineValidationError> errors)
        {
            if (errors == null || errors.Count == 0)
            {
                _errors = Array.Empty<AbilityEffectTimelineValidationError>();
                _errorsView = Array.AsReadOnly(_errors);
                return;
            }

            _errors = new AbilityEffectTimelineValidationError[errors.Count];
            for (int i = 0; i < errors.Count; i++)
                _errors[i] = errors[i];

            _errorsView = Array.AsReadOnly(_errors);
        }

        public bool IsValid => _errors.Length == 0;
        public int ErrorCount => _errors.Length;
        public IReadOnlyList<AbilityEffectTimelineValidationError> Errors => _errorsView;

        public bool Contains(AbilityEffectTimelineValidationErrorCode code)
        {
            for (int i = 0; i < _errors.Length; i++)
            {
                if (_errors[i].Code == code)
                    return true;
            }

            return false;
        }
    }

    public static class AbilityEffectTimelineValidator
    {
        public static AbilityEffectTimelineValidationResult Validate(AbilityEffectTimelineDefinition timeline)
        {
            var errors = new List<AbilityEffectTimelineValidationError>();
            if (timeline == null)
            {
                Add(errors, AbilityEffectTimelineValidationErrorCode.NullTimeline, "Ability effect timeline definition is null.");
                return new AbilityEffectTimelineValidationResult(errors);
            }

            ValidateTimeline(timeline, errors);
            return new AbilityEffectTimelineValidationResult(errors);
        }

        public static AbilityEffectTimelineValidationResult ValidateRegistry(
            IReadOnlyList<AbilityEffectTimelineDefinition> timelines,
            int maxChildDepth = AbilityEffectTimelineRuntimeModule.DefaultMaxChildDepth)
        {
            var errors = new List<AbilityEffectTimelineValidationError>();
            var byId = new Dictionary<int, AbilityEffectTimelineDefinition>();
            timelines ??= Array.Empty<AbilityEffectTimelineDefinition>();

            for (int i = 0; i < timelines.Count; i++)
            {
                AbilityEffectTimelineDefinition timeline = timelines[i];
                if (timeline == null)
                {
                    Add(errors, AbilityEffectTimelineValidationErrorCode.NullTimeline, "Ability effect timeline definition is null.", fieldPath: "timelines[" + i + "]");
                    continue;
                }

                ValidateTimeline(timeline, errors);
                if (timeline.TimelineId <= 0)
                    continue;
                if (byId.ContainsKey(timeline.TimelineId))
                {
                    Add(errors, AbilityEffectTimelineValidationErrorCode.DuplicateTimelineId, "Duplicate ability effect timeline id: " + timeline.TimelineId + ".", timeline.TimelineId, fieldPath: "timelines[" + i + "].timelineId");
                    continue;
                }

                byId.Add(timeline.TimelineId, timeline);
            }

            ValidateReferencesAndCycles(byId, maxChildDepth, errors);
            return new AbilityEffectTimelineValidationResult(errors);
        }

        private static void ValidateTimeline(
            AbilityEffectTimelineDefinition timeline,
            List<AbilityEffectTimelineValidationError> errors)
        {
            if (timeline.TimelineId <= 0)
                Add(errors, AbilityEffectTimelineValidationErrorCode.EmptyTimelineId, "Ability effect timeline id is required.", fieldPath: "timelineId");
            if (timeline.DurationFrames <= 0)
                Add(errors, AbilityEffectTimelineValidationErrorCode.InvalidDuration, "Ability effect timeline durationFrames must be positive.", timeline.TimelineId, fieldPath: Field(timeline, "durationFrames"));

            var eventIds = new HashSet<int>();
            for (int i = 0; i < timeline.Events.Count; i++)
            {
                AbilityEffectTimelineEvent evt = timeline.Events[i];
                string path = Field(timeline, "events[" + i + "]");
                if (evt.EventId <= 0)
                    Add(errors, AbilityEffectTimelineValidationErrorCode.EmptyEventId, "Ability effect timeline event id is required.", timeline.TimelineId, evt.EventId, i, path + ".eventId");
                else if (!eventIds.Add(evt.EventId))
                    Add(errors, AbilityEffectTimelineValidationErrorCode.DuplicateEventId, "Duplicate ability effect timeline event id: " + evt.EventId + ".", timeline.TimelineId, evt.EventId, i, path + ".eventId");

                if (evt.Frame < 0 || evt.Frame >= timeline.DurationFrames)
                    Add(errors, AbilityEffectTimelineValidationErrorCode.InvalidFrame, "Ability effect timeline event frame is outside duration.", timeline.TimelineId, evt.EventId, i, path + ".frame");
                if (evt.EndFrame < evt.Frame || evt.EndFrame >= timeline.DurationFrames)
                    Add(errors, AbilityEffectTimelineValidationErrorCode.InvalidEventRange, "Ability effect timeline event endFrame is outside duration or before frame.", timeline.TimelineId, evt.EventId, i, path + ".endFrame");
                if (!IsTrackEventPairValid(evt.TrackKind, evt.EventKind))
                    Add(errors, AbilityEffectTimelineValidationErrorCode.InvalidEventKind, "Ability effect timeline event kind does not belong to its track.", timeline.TimelineId, evt.EventId, i, path + ".eventKind");
                if (!IsShapeValid(evt))
                    Add(errors, AbilityEffectTimelineValidationErrorCode.InvalidShape, "Ability effect timeline combat event shape must be Sphere or Capsule with positive radius and non-zero layerMask.", timeline.TimelineId, evt.EventId, i, path + ".shape");
                if (evt.EventKind == AbilityEffectTimelineEventKind.StartEffectTimeline && evt.TargetTimelineId <= 0)
                    Add(errors, AbilityEffectTimelineValidationErrorCode.MissingTargetTimeline, "StartEffectTimeline event requires targetTimelineId.", timeline.TimelineId, evt.EventId, i, path + ".targetTimelineId");
            }
        }

        private static bool IsTrackEventPairValid(
            AbilityEffectTimelineTrackKind trackKind,
            AbilityEffectTimelineEventKind eventKind)
        {
            switch (trackKind)
            {
                case AbilityEffectTimelineTrackKind.Combat:
                    return eventKind == AbilityEffectTimelineEventKind.AreaPulse
                        || eventKind == AbilityEffectTimelineEventKind.ShapeQueryWindow;
                case AbilityEffectTimelineTrackKind.Gameplay:
                    return eventKind == AbilityEffectTimelineEventKind.ApplyEffect
                        || eventKind == AbilityEffectTimelineEventKind.SpawnProjectile
                        || eventKind == AbilityEffectTimelineEventKind.StartEffectTimeline
                        || eventKind == AbilityEffectTimelineEventKind.EmitGameplayEvent;
                case AbilityEffectTimelineTrackKind.Presentation:
                    return eventKind == AbilityEffectTimelineEventKind.SpawnVfx
                        || eventKind == AbilityEffectTimelineEventKind.PlayAudioCue
                        || eventKind == AbilityEffectTimelineEventKind.CameraImpulse
                        || eventKind == AbilityEffectTimelineEventKind.UiFeedback;
                default:
                    return false;
            }
        }

        private static bool IsShapeValid(AbilityEffectTimelineEvent evt)
        {
            if (evt.TrackKind != AbilityEffectTimelineTrackKind.Combat)
                return evt.ShapeKind == AbilityEffectTimelineShapeKind.None && evt.Radius == Fix64.Zero;

            if (evt.ShapeKind != AbilityEffectTimelineShapeKind.Sphere
                && evt.ShapeKind != AbilityEffectTimelineShapeKind.Capsule)
            {
                return false;
            }

            return evt.Radius > Fix64.Zero && evt.LayerMask != 0;
        }

        private static void ValidateReferencesAndCycles(
            Dictionary<int, AbilityEffectTimelineDefinition> byId,
            int maxChildDepth,
            List<AbilityEffectTimelineValidationError> errors)
        {
            foreach (KeyValuePair<int, AbilityEffectTimelineDefinition> pair in byId)
            {
                for (int i = 0; i < pair.Value.Events.Count; i++)
                {
                    AbilityEffectTimelineEvent evt = pair.Value.Events[i];
                    if (evt.EventKind != AbilityEffectTimelineEventKind.StartEffectTimeline)
                        continue;

                    if (!byId.ContainsKey(evt.TargetTimelineId))
                    {
                        Add(errors, AbilityEffectTimelineValidationErrorCode.MissingReferencedTimeline, "Referenced ability effect timeline is missing: " + evt.TargetTimelineId + ".", pair.Key, evt.EventId, i, Field(pair.Value, "events[" + i + "].targetTimelineId"));
                    }
                }
            }

            var path = new List<int>();
            var visiting = new HashSet<int>();
            var visited = new HashSet<int>();
            foreach (int timelineId in byId.Keys)
            {
                if (Visit(timelineId, byId, maxChildDepth, visiting, visited, path, errors))
                    return;
            }
        }

        private static bool Visit(
            int timelineId,
            Dictionary<int, AbilityEffectTimelineDefinition> byId,
            int maxChildDepth,
            HashSet<int> visiting,
            HashSet<int> visited,
            List<int> path,
            List<AbilityEffectTimelineValidationError> errors)
        {
            if (visited.Contains(timelineId))
                return false;
            if (visiting.Contains(timelineId))
            {
                Add(errors, AbilityEffectTimelineValidationErrorCode.TimelineCycleDetected, "Ability effect timeline child graph contains a cycle at: " + timelineId + ".", timelineId, fieldPath: "effectTimelines");
                return true;
            }
            if (path.Count > maxChildDepth)
            {
                Add(errors, AbilityEffectTimelineValidationErrorCode.MaxChildDepthExceeded, "Ability effect timeline child graph exceeds max child depth: " + maxChildDepth + ".", timelineId, fieldPath: "effectTimelines");
                return true;
            }

            if (!byId.TryGetValue(timelineId, out AbilityEffectTimelineDefinition timeline))
                return false;

            visiting.Add(timelineId);
            path.Add(timelineId);
            for (int i = 0; i < timeline.Events.Count; i++)
            {
                AbilityEffectTimelineEvent evt = timeline.Events[i];
                if (evt.EventKind == AbilityEffectTimelineEventKind.StartEffectTimeline
                    && byId.ContainsKey(evt.TargetTimelineId)
                    && Visit(evt.TargetTimelineId, byId, maxChildDepth, visiting, visited, path, errors))
                {
                    return true;
                }
            }

            path.RemoveAt(path.Count - 1);
            visiting.Remove(timelineId);
            visited.Add(timelineId);
            return false;
        }

        private static string Field(AbilityEffectTimelineDefinition timeline, string field)
        {
            return timeline == null || timeline.TimelineId <= 0
                ? field
                : "effectTimelines[" + timeline.TimelineId + "]." + field;
        }

        private static void Add(
            List<AbilityEffectTimelineValidationError> errors,
            AbilityEffectTimelineValidationErrorCode code,
            string message,
            int timelineId = 0,
            int eventId = 0,
            int eventIndex = -1,
            string fieldPath = "")
        {
            errors.Add(new AbilityEffectTimelineValidationError(code, message, timelineId, eventId, eventIndex, fieldPath));
        }
    }

    public readonly struct AbilityEffectTimelineContext
    {
        public AbilityEffectTimelineContext(
            int abilityId,
            int effectTimelineId,
            int ownerEntityId,
            int sourceEntityId,
            int triggerEntityId,
            AbilityEffectTimelineTriggerKind triggerKind,
            FixVector3 anchorPosition,
            FixVector3 anchorDirection,
            int parentInstanceId = 0)
        {
            if (!Enum.IsDefined(typeof(AbilityEffectTimelineTriggerKind), triggerKind))
                throw new ArgumentOutOfRangeException(nameof(triggerKind), "Ability effect timeline trigger kind is not supported.");

            AbilityId = abilityId < 0 ? 0 : abilityId;
            EffectTimelineId = effectTimelineId;
            OwnerEntityId = ownerEntityId < 0 ? 0 : ownerEntityId;
            SourceEntityId = sourceEntityId < 0 ? 0 : sourceEntityId;
            TriggerEntityId = triggerEntityId < 0 ? 0 : triggerEntityId;
            TriggerKind = triggerKind;
            AnchorPosition = anchorPosition;
            AnchorDirection = anchorDirection.TryNormalize(out FixVector3 normalized)
                ? normalized
                : new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One);
            ParentInstanceId = parentInstanceId < 0 ? 0 : parentInstanceId;
        }

        public int AbilityId { get; }
        public int EffectTimelineId { get; }
        public int OwnerEntityId { get; }
        public int SourceEntityId { get; }
        public int TriggerEntityId { get; }
        public AbilityEffectTimelineTriggerKind TriggerKind { get; }
        public FixVector3 AnchorPosition { get; }
        public FixVector3 AnchorDirection { get; }
        public int ParentInstanceId { get; }

        public AbilityEffectTimelineContext WithTimeline(
            int effectTimelineId,
            AbilityEffectTimelineTriggerKind triggerKind,
            int parentInstanceId,
            FixVector3 anchorPosition)
        {
            return new AbilityEffectTimelineContext(
                AbilityId,
                effectTimelineId,
                OwnerEntityId,
                SourceEntityId,
                TriggerEntityId,
                triggerKind,
                anchorPosition,
                AnchorDirection,
                parentInstanceId);
        }
    }

    public readonly struct AbilityEffectTimelineEventDispatch
    {
        public AbilityEffectTimelineEventDispatch(
            int instanceId,
            RuntimeFrame runtimeFrame,
            int localFrame,
            AbilityEffectTimelineContext context,
            AbilityEffectTimelineEvent evt,
            FixVector3 anchorPosition,
            int childDepth)
        {
            InstanceId = instanceId;
            RuntimeFrame = runtimeFrame;
            LocalFrame = localFrame;
            Context = context;
            Event = evt;
            AnchorPosition = anchorPosition;
            ChildDepth = childDepth < 0 ? 0 : childDepth;
        }

        public int InstanceId { get; }
        public RuntimeFrame RuntimeFrame { get; }
        public int LocalFrame { get; }
        public AbilityEffectTimelineContext Context { get; }
        public AbilityEffectTimelineEvent Event { get; }
        public FixVector3 AnchorPosition { get; }
        public int ChildDepth { get; }
    }

    public readonly struct AbilityEffectTimelineAnchorQuery
    {
        public AbilityEffectTimelineAnchorQuery(
            AbilityEffectTimelineContext context,
            AbilityEffectTimelineEvent evt,
            int instanceId,
            int localFrame)
        {
            Context = context;
            Event = evt;
            InstanceId = instanceId;
            LocalFrame = localFrame < 0 ? 0 : localFrame;
        }

        public AbilityEffectTimelineContext Context { get; }
        public AbilityEffectTimelineEvent Event { get; }
        public int InstanceId { get; }
        public int LocalFrame { get; }
    }

    public interface IAbilityEffectTimelineAnchorResolver
    {
        bool TryResolveAbilityEffectTimelineAnchor(
            AbilityEffectTimelineAnchorQuery query,
            out FixVector3 anchorPosition);
    }

    /// <summary>Optional synchronous birth admission. Receives the actual reserved provider identity before publication.</summary>
    public interface IAbilityEffectTimelineStartGate
    {
        /// <remarks>NoAlloc after owner buffers warm. A rejection leaves the timeline allocator and queues unchanged.</remarks>
        bool TryAcceptTimelineStart(int instanceId, in AbilityEffectTimelineContext context, RuntimeFrame frame);
    }

    public interface IAbilityEffectTimelineEventSink
    {
        void OnAbilityEffectTimelineEvent(AbilityEffectTimelineEventDispatch dispatch);
    }

    public readonly struct AbilityEffectTimelineStartResult
    {
        public AbilityEffectTimelineStartResult(bool success, int instanceId, string failureReason)
        {
            Success = success;
            InstanceId = instanceId < 0 ? 0 : instanceId;
            FailureReason = failureReason ?? string.Empty;
        }

        public bool Success { get; }
        public int InstanceId { get; }
        public string FailureReason { get; }
    }

    public readonly struct AbilityEffectTimelineInstanceSnapshot
    {
        public AbilityEffectTimelineInstanceSnapshot(
            int instanceId,
            int timelineId,
            int abilityId,
            int ownerEntityId,
            int sourceEntityId,
            int triggerEntityId,
            int parentInstanceId,
            int localFrame,
            RuntimeFrame startFrame,
            int childDepth)
        {
            InstanceId = instanceId;
            TimelineId = timelineId;
            AbilityId = abilityId;
            OwnerEntityId = ownerEntityId;
            SourceEntityId = sourceEntityId;
            TriggerEntityId = triggerEntityId;
            ParentInstanceId = parentInstanceId;
            LocalFrame = localFrame;
            StartFrame = startFrame;
            ChildDepth = childDepth;
        }

        public int InstanceId { get; }
        public int TimelineId { get; }
        public int AbilityId { get; }
        public int OwnerEntityId { get; }
        public int SourceEntityId { get; }
        public int TriggerEntityId { get; }
        public int ParentInstanceId { get; }
        public int LocalFrame { get; }
        public RuntimeFrame StartFrame { get; }
        public int ChildDepth { get; }
    }

    public sealed class AbilityEffectTimelineRuntimeModule : RuntimeModule
    {
        public const int DefaultModuleIdentity = 3203;
        public int ModuleIdentity { get; }
        public const int DefaultMaxChildDepth = 8;
        public const string DefaultModuleId = "mxframework.ability.effectTimeline.runtime";

        private readonly List<ActiveInstance> _instances = new List<ActiveInstance>();
        private readonly List<ActiveInstance> _pendingStarts = new List<ActiveInstance>();
        private readonly IAbilityEffectTimelineEventSink _eventSink;
        private readonly IAbilityEffectTimelineAnchorResolver _anchorResolver;
        private int _nextInstanceId = 1;

        public AbilityEffectTimelineRuntimeModule(
            AbilityEffectTimelineRegistry timelines,
            IAbilityEffectTimelineEventSink eventSink = null,
            string moduleId = DefaultModuleId,
            RuntimeTickStage tickStage = RuntimeTickStage.Simulation,
            int priority = 95,
            int maxChildDepth = DefaultMaxChildDepth,
            IAbilityEffectTimelineAnchorResolver anchorResolver = null,
            int moduleIdentity = DefaultModuleIdentity)
            : base(moduleId, tickStage, priority)
        {
            if (moduleIdentity <= 0) throw new ArgumentOutOfRangeException(nameof(moduleIdentity));
            ModuleIdentity = moduleIdentity;
            Timelines = timelines ?? throw new ArgumentNullException(nameof(timelines));
            _eventSink = eventSink;
            _anchorResolver = anchorResolver;
            MaxChildDepth = maxChildDepth < 0 ? DefaultMaxChildDepth : maxChildDepth;
        }

        public AbilityEffectTimelineRegistry Timelines { get; }
        public int MaxChildDepth { get; }
        public int ActiveInstanceCount => _instances.Count;
        public int NextInstanceId => _nextInstanceId;
        public bool IsIdle => _instances.Count == 0 && _pendingStarts.Count == 0;

        /// <summary>Restores the allocator only into a fresh, idle runtime. The caller supplies trusted saved state.</summary>
        /// <remarks>NoAlloc. Never rewinds an already-used runtime or imports active instances.</remarks>
        public bool TryRestoreIdleIdentity(int nextInstanceId)
        {
            if (!IsIdle || _nextInstanceId != 1 || nextInstanceId <= 0) return false;
            _nextInstanceId = nextInstanceId; return true;
        }

        public bool ContainsActiveInstance(int instanceId)
        {
            if (instanceId <= 0)
                return false;

            FlushPendingStarts();
            for (int i = 0; i < _instances.Count; i++)
            {
                if (_instances[i].InstanceId == instanceId)
                    return true;
            }

            return false;
        }

        public AbilityEffectTimelineStartResult StartTimeline(
            AbilityEffectTimelineContext context,
            RuntimeFrame frame,
            int childDepth = 0)
        {
            if (context.EffectTimelineId <= 0)
                return new AbilityEffectTimelineStartResult(false, 0, "MissingEffectTimelineId");
            if (childDepth > MaxChildDepth)
                return new AbilityEffectTimelineStartResult(false, 0, "MaxChildDepthExceeded");
            if (!Timelines.TryGet(context.EffectTimelineId, out AbilityEffectTimelineDefinition timeline))
                return new AbilityEffectTimelineStartResult(false, 0, "MissingEffectTimeline");

            if (_nextInstanceId <= 0 || _nextInstanceId == int.MaxValue)
                return new AbilityEffectTimelineStartResult(false, 0, "InstanceIdentityExhausted");
            int instanceId = _nextInstanceId;
            if (_eventSink is IAbilityEffectTimelineStartGate gate && !gate.TryAcceptTimelineStart(instanceId, context, frame))
                return new AbilityEffectTimelineStartResult(false, 0, "StartGateRejected");
            _nextInstanceId++;
            var instance = new ActiveInstance(instanceId, timeline, context, frame, childDepth);
            _pendingStarts.Add(instance);
            return new AbilityEffectTimelineStartResult(true, instanceId, string.Empty);
        }

        /// <remarks>NoAlloc on the steady-state path after registration/initialization; invalid-call and exception paths may allocate diagnostics.</remarks>
        public override void Tick(RuntimeTickContext context)
        {
            FlushPendingStarts();
            RuntimeFrame frame = new RuntimeFrame(context.FrameIndex);
            for (int i = _instances.Count - 1; i >= 0; i--)
            {
                ActiveInstance instance = _instances[i];
                TickInstance(instance, frame);
                if (instance.IsComplete)
                    _instances.RemoveAt(i);
            }

            FlushPendingStarts();
        }

        /// <summary>Runs only newly born frame-zero events at this exact simulation frame.
        /// Existing timelines retain their local frame. The caller repeats causal rounds before advancing time.</summary>
        /// <remarks>NoAlloc after provider capacity warms; deterministic budget failure does not defer remaining work.</remarks>
        public int PumpFrameZero(RuntimeFrame frame, int maximumInstances = 256)
        {
            if (maximumInstances <= 0) throw new ArgumentOutOfRangeException(nameof(maximumInstances));
            int advanced = 0;
            bool again;
            do {
                again = false; FlushPendingStarts();
                for (int i = _instances.Count - 1; i >= 0; i--) {
                    var instance = _instances[i];
                    if (instance.LocalFrame != 0 || instance.StartFrame.Value != frame.Value) continue;
                    if (advanced >= maximumInstances) throw new InvalidOperationException("timeline:FrameZeroBudgetExceeded");
                    TickInstance(instance, frame); advanced++; again = true;
                    if (instance.IsComplete) _instances.RemoveAt(i);
                }
            } while (again && _pendingStarts.Count != 0);
            return advanced;
        }

        private static readonly Comparison<AbilityEffectTimelineInstanceSnapshot> HashInstanceComparison =
            (left, right) => left.InstanceId.CompareTo(right.InstanceId);

        public void CopyStateTo(List<AbilityEffectTimelineInstanceSnapshot> output)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));
            FlushPendingStarts();
            for (int i = 0; i < _instances.Count; i++) output.Add(_instances[i].CreateSnapshot());
            output.Sort(HashInstanceComparison);
        }

        public AbilityEffectTimelineInstanceSnapshot[] CreateSnapshot()
        {
            FlushPendingStarts();
            var result = new AbilityEffectTimelineInstanceSnapshot[_instances.Count];
            for (int i = 0; i < _instances.Count; i++)
                result[i] = _instances[i].CreateSnapshot();

            Array.Sort(result, (left, right) => left.InstanceId.CompareTo(right.InstanceId));
            return result;
        }

        private void TickInstance(ActiveInstance instance, RuntimeFrame frame)
        {
            IReadOnlyList<AbilityEffectTimelineEvent> events = instance.Timeline.Events;
            while (instance.NextEventIndex < events.Count
                && events[instance.NextEventIndex].Frame <= instance.LocalFrame)
            {
                AbilityEffectTimelineEvent evt = events[instance.NextEventIndex];
                instance.NextEventIndex++;
                if (evt.Frame != instance.LocalFrame)
                    continue;
                if (ShouldSkipForMissingContext(instance.Context, evt))
                    continue;

                FixVector3 anchor = ResolveEventAnchor(instance, evt);
                var dispatch = new AbilityEffectTimelineEventDispatch(
                    instance.InstanceId,
                    frame,
                    instance.LocalFrame,
                    instance.Context,
                    evt,
                    anchor,
                    instance.ChildDepth);
                _eventSink?.OnAbilityEffectTimelineEvent(dispatch);
                if (evt.EventKind == AbilityEffectTimelineEventKind.StartEffectTimeline)
                    StartChildTimeline(instance, evt, frame, anchor);
            }

            instance.LocalFrame++;
            if (instance.LocalFrame >= instance.Timeline.DurationFrames)
                instance.IsComplete = true;
        }

        private void StartChildTimeline(
            ActiveInstance parent,
            AbilityEffectTimelineEvent evt,
            RuntimeFrame frame,
            FixVector3 anchor)
        {
            AbilityEffectTimelineContext childContext = parent.Context.WithTimeline(
                evt.TargetTimelineId,
                AbilityEffectTimelineTriggerKind.EffectTimelineEvent,
                parent.InstanceId,
                anchor);
            StartTimeline(childContext, frame, parent.ChildDepth + 1);
        }

        private void FlushPendingStarts()
        {
            if (_pendingStarts.Count == 0)
                return;

            for (int i = 0; i < _pendingStarts.Count; i++)
                _instances.Add(_pendingStarts[i]);
            _pendingStarts.Clear();
        }

        private FixVector3 ResolveEventAnchor(
            ActiveInstance instance,
            AbilityEffectTimelineEvent evt)
        {
            AbilityEffectTimelineContext context = instance.Context;
            FixVector3 anchor = context.AnchorPosition;
            if (evt.AnchorMode != AbilityEffectTimelineAnchorMode.CapturedAnchor
                && _anchorResolver != null)
            {
                var query = new AbilityEffectTimelineAnchorQuery(
                    context,
                    evt,
                    instance.InstanceId,
                    instance.LocalFrame);
                if (_anchorResolver.TryResolveAbilityEffectTimelineAnchor(query, out FixVector3 resolved))
                    anchor = resolved;
            }

            return anchor + evt.LocalOffset;
        }

        private static bool ShouldSkipForMissingContext(
            AbilityEffectTimelineContext context,
            AbilityEffectTimelineEvent evt)
        {
            if (evt.RequiresOwner && context.OwnerEntityId == 0)
                return true;
            return evt.RequiresSource && context.SourceEntityId == 0;
        }

        private sealed class ActiveInstance
        {
            public ActiveInstance(
                int instanceId,
                AbilityEffectTimelineDefinition timeline,
                AbilityEffectTimelineContext context,
                RuntimeFrame startFrame,
                int childDepth)
            {
                InstanceId = instanceId;
                Timeline = timeline;
                Context = context;
                StartFrame = startFrame;
                ChildDepth = childDepth < 0 ? 0 : childDepth;
            }

            public int InstanceId { get; }
            public AbilityEffectTimelineDefinition Timeline { get; }
            public AbilityEffectTimelineContext Context { get; }
            public RuntimeFrame StartFrame { get; }
            public int ChildDepth { get; }
            public int LocalFrame { get; set; }
            public int NextEventIndex { get; set; }
            public bool IsComplete { get; set; }

            public AbilityEffectTimelineInstanceSnapshot CreateSnapshot()
            {
                return new AbilityEffectTimelineInstanceSnapshot(
                    InstanceId,
                    Timeline.TimelineId,
                    Context.AbilityId,
                    Context.OwnerEntityId,
                    Context.SourceEntityId,
                    Context.TriggerEntityId,
                    Context.ParentInstanceId,
                    LocalFrame,
                    StartFrame,
                    ChildDepth);
            }
        }
    }

    public sealed class AbilityEffectTimelineRuntimeHashContributor : IRuntimeHashContributor
    {
        private const int F_identityMapping = 14;
        private const int F_moduleId = 1;
        private const int F_definition_hash = 2;
        private const int F_instance_count = 3;
        private const int F_instance_instanceId = 4;
        private const int F_instance_timelineId = 5;
        private const int F_instance_abilityId = 6;
        private const int F_instance_ownerEntityId = 7;
        private const int F_instance_sourceEntityId = 8;
        private const int F_instance_triggerEntityId = 9;
        private const int F_instance_parentInstanceId = 10;
        private const int F_instance_localFrame = 11;
        private const int F_instance_startFrame = 12;
        private const int F_instance_childDepth = 13;

        private readonly AbilityEffectTimelineRuntimeModule _module;
        private readonly List<AbilityEffectTimelineInstanceSnapshot> _instances = new List<AbilityEffectTimelineInstanceSnapshot>();

        public AbilityEffectTimelineRuntimeHashContributor(
            AbilityEffectTimelineRuntimeModule module,
            int contributorId = RuntimeHashContributorIds.AbilityEffectTimeline)
        {
            _module = module ?? throw new ArgumentNullException(nameof(module));
            if (contributorId <= 0) throw new ArgumentOutOfRangeException(nameof(contributorId));
            ContributorId = contributorId;
        }

        public int HashSchemaId => RuntimeHashSchemaIds.AbilityEffectTimeline;
        public int HashSchemaVersion => 2;
        public int ContributorId { get; }

        public void Contribute(RuntimeHashContext context, RuntimeHashAccumulator accumulator)
        {
            if (accumulator == null)
                throw new ArgumentNullException(nameof(accumulator));

            accumulator.AddInt(F_moduleId, _module.ModuleIdentity);
            accumulator.AddInt(15, _module.NextInstanceId);
            accumulator.AddLong(F_definition_hash, _module.Timelines.PreparedContentHash);
            accumulator.AddLong(F_identityMapping, _module.Timelines.PreparedIdentityHash);
            _instances.Clear();
            _module.CopyStateTo(_instances);
            List<AbilityEffectTimelineInstanceSnapshot> instances = _instances;
            accumulator.BeginArray(F_instance_count, instances.Count);
            for (int i = 0; i < instances.Count; i++)
            {
                AbilityEffectTimelineInstanceSnapshot instance = instances[i];
                accumulator.BeginElement(i);
                accumulator.AddInt(F_instance_instanceId, instance.InstanceId);
                accumulator.AddInt(F_instance_timelineId, instance.TimelineId);
                accumulator.AddInt(F_instance_abilityId, instance.AbilityId);
                accumulator.AddInt(F_instance_ownerEntityId, instance.OwnerEntityId);
                accumulator.AddInt(F_instance_sourceEntityId, instance.SourceEntityId);
                accumulator.AddInt(F_instance_triggerEntityId, instance.TriggerEntityId);
                accumulator.AddInt(F_instance_parentInstanceId, instance.ParentInstanceId);
                accumulator.AddInt(F_instance_localFrame, instance.LocalFrame);
                accumulator.AddLong(F_instance_startFrame, instance.StartFrame.Value);
                accumulator.AddInt(F_instance_childDepth, instance.ChildDepth);
                accumulator.EndElement();
            }
            accumulator.EndArray();
        }
    }

    public static class AbilityEffectTimelineJsonImporter
    {
        public const string CurrentSchemaVersion = "2";

        public static AbilityEffectTimelineRegistry ImportFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return new AbilityEffectTimelineRegistry();

            return Import(File.ReadAllText(path), path);
        }

        public static AbilityEffectTimelineRegistry Import(string json, string sourcePath = "")
        {
            var registry = new AbilityEffectTimelineRegistry();
            if (string.IsNullOrWhiteSpace(json))
                return registry;

            JObject root = JObject.Parse(json);
            JArray timelines = root["timelines"] as JArray;
            var definitions = new List<AbilityEffectTimelineDefinition>();
            for (int i = 0; timelines != null && i < timelines.Count; i++)
            {
                if (!(timelines[i] is JObject fields))
                    continue;

                definitions.Add(ReadTimeline(fields));
            }

            AbilityEffectTimelineValidationResult validation =
                AbilityEffectTimelineValidator.ValidateRegistry(definitions);
            if (!validation.IsValid)
            {
                AbilityEffectTimelineValidationError error = validation.Errors[0];
                throw new InvalidOperationException(
                    "Invalid ability effect timeline at "
                    + (sourcePath ?? string.Empty)
                    + ": "
                    + error.Code
                    + " "
                    + error.FieldPath
                    + " - "
                    + error.Message);
            }

            for (int i = 0; i < definitions.Count; i++)
            {
                if (!registry.TryRegister(definitions[i], out string failure))
                    throw new InvalidOperationException("Invalid ability effect timeline at " + (sourcePath ?? string.Empty) + "#" + i + ": " + failure);
            }

            return registry;
        }

        private static AbilityEffectTimelineDefinition ReadTimeline(JObject fields)
        {
            return new AbilityEffectTimelineDefinition(
                ReadDefinitionId(fields, "timelineId"),
                ReadInt(fields, "durationFrames"),
                ReadEvents(fields["events"] as JArray),
                ReadString(fields, "timelineLabel"));
        }

        private static AbilityEffectTimelineEvent[] ReadEvents(JArray array)
        {
            if (array == null || array.Count == 0)
                return Array.Empty<AbilityEffectTimelineEvent>();

            var events = new AbilityEffectTimelineEvent[array.Count];
            for (int i = 0; i < array.Count; i++)
                events[i] = ReadEvent(array[i] as JObject);

            return events;
        }

        private static AbilityEffectTimelineEvent ReadEvent(JObject fields)
        {
            fields ??= new JObject();
            return new AbilityEffectTimelineEvent(
                ReadDefinitionId(fields, "eventId"),
                ReadEnum(fields, "trackKind", AbilityEffectTimelineTrackKind.Gameplay),
                ReadEnum(fields, "eventKind", AbilityEffectTimelineEventKind.EmitGameplayEvent),
                ReadInt(fields, "frame"),
                ReadInt(fields, "endFrame", ReadInt(fields, "frame")),
                ReadEnum(fields, "shapeKind", AbilityEffectTimelineShapeKind.None),
                ReadEnum(fields, "anchorMode", AbilityEffectTimelineAnchorMode.CapturedAnchor),
                ReadVector(fields["localOffset"] as JObject),
                ReadVector(fields["localPointB"] as JObject),
                ToFix64(ReadFloat(fields, "radius")),
                ReadInt(fields, "layerMask"),
                ReadDefinitionId(fields, "targetTimelineId"),
                ReadDefinitionId(fields, "requestId"),
                ReadInt(fields, "effectId"),
                ReadDefinitionId(fields, "presentationProfileId"),
                ReadBool(fields, "requiresOwner"),
                ReadBool(fields, "requiresSource"),
                ReadString(fields, "eventLabel"),
                ReadString(fields, "targetTimelineLabel"),
                ReadString(fields, "requestLabel"),
                ReadString(fields, "presentationProfileLabel"));
        }

        private static int ReadDefinitionId(JObject fields, string name)
        {
            JToken token = fields?[name];
            if (token == null) return 0;
            if (token.Type != JTokenType.Integer)
                throw new ArgumentException("Effect timeline requires an authored integer identity: " + name);
            int value = token.Value<int>();
            if (value < 0) throw new ArgumentException("Effect timeline identity cannot be negative: " + name);
            return value;
        }

        private static FixVector3 ReadVector(JObject fields)
        {
            if (fields == null)
                return FixVector3.Zero;

            return new FixVector3(
                ToFix64(ReadFloat(fields, "x")),
                ToFix64(ReadFloat(fields, "y")),
                ToFix64(ReadFloat(fields, "z")));
        }

        private static TEnum ReadEnum<TEnum>(JObject fields, string name, TEnum fallback)
            where TEnum : struct
        {
            string raw = ReadString(fields, name);
            if (string.IsNullOrWhiteSpace(raw))
                return fallback;
            if (Enum.TryParse(raw, false, out TEnum parsed)
                && Enum.IsDefined(typeof(TEnum), parsed))
            {
                return parsed;
            }

            throw new ArgumentException("Unsupported enum value for " + name + ": " + raw + ".");
        }

        private static Fix64 ToFix64(float value)
        {
            return Fix64.FromRaw(checked((long)Math.Round(value * Fix64.Scale, MidpointRounding.AwayFromZero)));
        }

        private static string ReadString(JObject fields, string name, string fallback = "")
        {
            if (fields == null || string.IsNullOrWhiteSpace(name))
                return fallback ?? string.Empty;

            JToken token = fields[name];
            return token == null ? fallback ?? string.Empty : token.Value<string>() ?? fallback ?? string.Empty;
        }

        private static int ReadInt(JObject fields, string name, int fallback = 0)
        {
            if (fields == null || string.IsNullOrWhiteSpace(name))
                return fallback;

            JToken token = fields[name];
            return token == null ? fallback : token.Value<int>();
        }

        private static float ReadFloat(JObject fields, string name, float fallback = 0f)
        {
            if (fields == null || string.IsNullOrWhiteSpace(name))
                return fallback;

            JToken token = fields[name];
            return token == null ? fallback : token.Value<float>();
        }

        private static bool ReadBool(JObject fields, string name, bool fallback = false)
        {
            if (fields == null || string.IsNullOrWhiteSpace(name))
                return fallback;

            JToken token = fields[name];
            return token == null ? fallback : token.Value<bool>();
        }
    }
}

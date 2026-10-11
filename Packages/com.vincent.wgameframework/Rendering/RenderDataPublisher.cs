using System;
using System.Collections.Generic;
using System.Text;
using MxFramework.Diagnostics;
using UnityEngine;

namespace MxFramework.Rendering
{
    public enum RenderDataEventKind
    {
        Impact = 0,
        SurfaceContact = 1,
        FieldImpulse = 2,
        Movement = 3,
        Lifecycle = 4
    }

    public interface IRenderDataPublisher
    {
        void PublishImpact(MxRenderSubjectId subject, in MxRenderImpactEvent impact);
        void PublishSurfaceContact(MxRenderSubjectId subject, in MxRenderSurfaceContactEvent contact);
        void PublishFieldImpulse(MxRenderSubjectId subject, in MxRenderFieldImpulseEvent impulse);
        void PublishSubjectMovement(MxRenderSubjectId subject, Vector3 velocity);
        void PublishSubjectLifecycle(MxRenderSubjectId subject, MxSubjectLifecycleKind lifecycle);
    }

    public sealed class RenderDataPublisher : IRenderDataPublisher
    {
        private const int EventKindCount = 5;

        private readonly MxRenderSubjectRegistry _subjects;
        private readonly int _recentCapacity;
        private readonly List<RenderDataEvent> _currentFrameEvents = new List<RenderDataEvent>();
        private readonly List<RenderDataEvent> _recentEvents = new List<RenderDataEvent>();
        private readonly int[] _currentFrameCounts = new int[EventKindCount];
        private readonly int[] _recentCounts = new int[EventKindCount];
        private readonly int[] _totalCounts = new int[EventKindCount];

        public RenderDataPublisher(MxRenderSubjectRegistry subjects, int recentCapacity = 32)
        {
            _subjects = subjects ?? throw new ArgumentNullException(nameof(subjects));
            _recentCapacity = Math.Max(0, recentCapacity);
            _subjects.SubjectReleased += ClearSubject;
        }

        public void PublishImpact(MxRenderSubjectId subject, in MxRenderImpactEvent impact)
        {
            PublishEvent(new RenderDataEvent(
                subject,
                RenderDataEventKind.Impact,
                impact.WorldPosition,
                Vector3.zero,
                impact.Intensity,
                impact.Duration,
                0,
                impact.Tint,
                0f,
                MxSubjectLifecycleKind.Spawned));
        }

        public void PublishSurfaceContact(MxRenderSubjectId subject, in MxRenderSurfaceContactEvent contact)
        {
            PublishEvent(new RenderDataEvent(
                subject,
                RenderDataEventKind.SurfaceContact,
                contact.WorldPosition,
                Vector3.zero,
                contact.Pressure,
                0f,
                0,
                default,
                contact.Radius,
                MxSubjectLifecycleKind.Spawned));
        }

        public void PublishFieldImpulse(MxRenderSubjectId subject, in MxRenderFieldImpulseEvent impulse)
        {
            PublishEvent(new RenderDataEvent(
                subject,
                RenderDataEventKind.FieldImpulse,
                impulse.WorldPosition,
                Vector3.zero,
                impulse.Intensity,
                0f,
                0,
                default,
                impulse.Radius,
                MxSubjectLifecycleKind.Spawned,
                impulse.ChannelId));
        }

        public void PublishSubjectMovement(MxRenderSubjectId subject, Vector3 velocity)
        {
            PublishEvent(new RenderDataEvent(
                subject,
                RenderDataEventKind.Movement,
                Vector3.zero,
                velocity));
        }

        public void PublishSubjectLifecycle(MxRenderSubjectId subject, MxSubjectLifecycleKind lifecycle)
        {
            if (!Enum.IsDefined(typeof(MxSubjectLifecycleKind), lifecycle))
                return;

            PublishEvent(new RenderDataEvent(
                subject,
                RenderDataEventKind.Lifecycle,
                Vector3.zero,
                Vector3.zero,
                0f,
                0f,
                0,
                default,
                0f,
                lifecycle));
        }

        public void BeginFrame()
        {
            for (int i = 0; i < _currentFrameEvents.Count; i++)
                ReleaseReference(_currentFrameEvents[i].Subject);

            _currentFrameEvents.Clear();
            Array.Clear(_currentFrameCounts, 0, _currentFrameCounts.Length);
        }

        private bool PublishEvent(in RenderDataEvent evt)
        {
            if (!IsKnownEventKind(evt.Kind) || !evt.Subject.IsValid)
                return false;

            if (!_subjects.TryResolve(evt.Subject, out var _))
                return false;
            _subjects.AddReference(evt.Subject);

            _currentFrameEvents.Add(evt);
            _currentFrameCounts[(int)evt.Kind]++;
            _totalCounts[(int)evt.Kind]++;
            AddRecent(evt);
            return true;
        }

        public void ClearSubject(MxRenderSubjectId subject)
        {
            if (!subject.IsValid)
                return;

            RemoveSubjectEvents(_currentFrameEvents, _currentFrameCounts, subject);
            RemoveSubjectEvents(_recentEvents, _recentCounts, subject);
        }

        public void Clear()
        {
            for (int i = 0; i < _currentFrameEvents.Count; i++)
                ReleaseReference(_currentFrameEvents[i].Subject);
            for (int i = 0; i < _recentEvents.Count; i++)
                ReleaseReference(_recentEvents[i].Subject);

            _currentFrameEvents.Clear();
            _recentEvents.Clear();
            Array.Clear(_currentFrameCounts, 0, _currentFrameCounts.Length);
            Array.Clear(_recentCounts, 0, _recentCounts.Length);
            Array.Clear(_totalCounts, 0, _totalCounts.Length);
        }

        public RenderDataPublisherSnapshot CaptureSnapshot()
        {
            return new RenderDataPublisherSnapshot(
                Sum(_currentFrameCounts),
                Sum(_recentCounts),
                Sum(_totalCounts),
                CreateCounts(_currentFrameCounts),
                CreateCounts(_recentCounts),
                CreateCounts(_totalCounts),
                _currentFrameEvents,
                _recentEvents);
        }

        private void AddRecent(in RenderDataEvent evt)
        {
            if (_recentCapacity <= 0)
                return;

            _subjects.AddReference(evt.Subject);

            _recentEvents.Add(evt);
            _recentCounts[(int)evt.Kind]++;

            while (_recentEvents.Count > _recentCapacity)
            {
                RenderDataEvent removed = _recentEvents[0];
                _recentEvents.RemoveAt(0);
                _recentCounts[(int)removed.Kind]--;
                ReleaseReference(removed.Subject);
            }
        }

        private void RemoveSubjectEvents(List<RenderDataEvent> events, int[] counts, MxRenderSubjectId subject)
        {
            for (int i = events.Count - 1; i >= 0; i--)
            {
                RenderDataEvent evt = events[i];
                if (evt.Subject != subject)
                    continue;

                events.RemoveAt(i);
                counts[(int)evt.Kind]--;
                ReleaseReference(evt.Subject);
            }
        }

        private void ReleaseReference(MxRenderSubjectId subject)
        {
            _subjects.ReleaseReference(subject);
        }

        private static IReadOnlyList<RenderDataEventKindCount> CreateCounts(int[] counts)
        {
            var values = new RenderDataEventKindCount[EventKindCount];
            for (int i = 0; i < EventKindCount; i++)
                values[i] = new RenderDataEventKindCount((RenderDataEventKind)i, counts[i]);
            return values;
        }

        private static bool IsKnownEventKind(RenderDataEventKind kind)
        {
            int value = (int)kind;
            return value >= 0 && value < EventKindCount;
        }

        private static int Sum(int[] counts)
        {
            int total = 0;
            for (int i = 0; i < counts.Length; i++)
                total += counts[i];
            return total;
        }
    }

    public readonly struct RenderDataEvent
    {
        public RenderDataEvent(
            MxRenderSubjectId subject,
            RenderDataEventKind kind,
            Vector3 worldPosition,
            Vector3 direction,
            float magnitude = 0f,
            float duration = 0f,
            int frame = 0,
            Color tint = default,
            float radius = 0f,
            MxSubjectLifecycleKind lifecycle = MxSubjectLifecycleKind.Spawned,
            int channelId = 0)
        {
            Subject = subject;
            Kind = kind;
            WorldPosition = worldPosition;
            Direction = direction;
            Magnitude = magnitude;
            Duration = duration;
            Frame = frame;
            Tint = tint;
            Radius = radius;
            Lifecycle = lifecycle;
            ChannelId = channelId;
        }

        public MxRenderSubjectId Subject { get; }
        public RenderDataEventKind Kind { get; }
        public Vector3 WorldPosition { get; }
        public Vector3 Direction { get; }
        public float Magnitude { get; }
        public float Duration { get; }
        public int Frame { get; }
        public Color Tint { get; }
        public float Radius { get; }
        public MxSubjectLifecycleKind Lifecycle { get; }
        public int ChannelId { get; }
    }

    public readonly struct MxRenderImpactEvent
    {
        public MxRenderImpactEvent(Vector3 worldPosition, Color tint, float intensity, float duration)
        {
            WorldPosition = worldPosition;
            Tint = tint;
            Intensity = intensity;
            Duration = duration;
        }

        public Vector3 WorldPosition { get; }
        public Color Tint { get; }
        public float Intensity { get; }
        public float Duration { get; }
    }

    public readonly struct MxRenderSurfaceContactEvent
    {
        public MxRenderSurfaceContactEvent(Vector3 worldPosition, float radius, float pressure)
        {
            WorldPosition = worldPosition;
            Radius = radius;
            Pressure = pressure;
        }

        public Vector3 WorldPosition { get; }
        public float Radius { get; }
        public float Pressure { get; }
    }

    public readonly struct MxRenderFieldImpulseEvent
    {
        public MxRenderFieldImpulseEvent(Vector3 worldPosition, float radius, float intensity, int channelId)
        {
            WorldPosition = worldPosition;
            Radius = radius;
            Intensity = intensity;
            ChannelId = channelId;
        }

        public Vector3 WorldPosition { get; }
        public float Radius { get; }
        public float Intensity { get; }
        public int ChannelId { get; }
    }

    public enum MxSubjectLifecycleKind
    {
        Spawned = 0,
        Despawned = 1,
        Disabled = 2,
        Enabled = 3
    }

    public readonly struct RenderDataEventKindCount
    {
        public RenderDataEventKindCount(RenderDataEventKind kind, int count)
        {
            Kind = kind;
            Count = count;
        }

        public RenderDataEventKind Kind { get; }
        public int Count { get; }
    }

    public sealed class RenderDataPublisherSnapshot
    {
        private readonly List<RenderDataEventKindCount> _currentFrameCounts;
        private readonly List<RenderDataEventKindCount> _recentCounts;
        private readonly List<RenderDataEventKindCount> _totalCounts;
        private readonly List<RenderDataEvent> _currentFrameEvents;
        private readonly List<RenderDataEvent> _recentEvents;

        public RenderDataPublisherSnapshot(
            int currentFrameEventCount,
            int recentEventCount,
            int totalEventCount,
            IReadOnlyList<RenderDataEventKindCount> currentFrameCounts,
            IReadOnlyList<RenderDataEventKindCount> recentCounts,
            IReadOnlyList<RenderDataEventKindCount> totalCounts,
            IReadOnlyList<RenderDataEvent> currentFrameEvents,
            IReadOnlyList<RenderDataEvent> recentEvents)
        {
            CurrentFrameEventCount = currentFrameEventCount;
            RecentEventCount = recentEventCount;
            TotalEventCount = totalEventCount;
            _currentFrameCounts = currentFrameCounts != null ? new List<RenderDataEventKindCount>(currentFrameCounts) : new List<RenderDataEventKindCount>();
            _recentCounts = recentCounts != null ? new List<RenderDataEventKindCount>(recentCounts) : new List<RenderDataEventKindCount>();
            _totalCounts = totalCounts != null ? new List<RenderDataEventKindCount>(totalCounts) : new List<RenderDataEventKindCount>();
            _currentFrameEvents = currentFrameEvents != null ? new List<RenderDataEvent>(currentFrameEvents) : new List<RenderDataEvent>();
            _recentEvents = recentEvents != null ? new List<RenderDataEvent>(recentEvents) : new List<RenderDataEvent>();
        }

        public int CurrentFrameEventCount { get; }
        public int RecentEventCount { get; }
        public int TotalEventCount { get; }
        public IReadOnlyList<RenderDataEventKindCount> CurrentFrameCounts => _currentFrameCounts;
        public IReadOnlyList<RenderDataEventKindCount> RecentCounts => _recentCounts;
        public IReadOnlyList<RenderDataEventKindCount> TotalCounts => _totalCounts;
        public IReadOnlyList<RenderDataEvent> CurrentFrameEvents => _currentFrameEvents;
        public IReadOnlyList<RenderDataEvent> RecentEvents => _recentEvents;

        public int CurrentFrameCount(RenderDataEventKind kind)
        {
            return CountFor(_currentFrameCounts, kind);
        }

        public int RecentCount(RenderDataEventKind kind)
        {
            return CountFor(_recentCounts, kind);
        }

        public int TotalCount(RenderDataEventKind kind)
        {
            return CountFor(_totalCounts, kind);
        }

        private static int CountFor(IReadOnlyList<RenderDataEventKindCount> counts, RenderDataEventKind kind)
        {
            for (int i = 0; i < counts.Count; i++)
            {
                if (counts[i].Kind == kind)
                    return counts[i].Count;
            }

            return 0;
        }
    }

    public sealed class RenderDataPublisherDebugSource : IRenderingDebugSource
    {
        private readonly RenderDataPublisher _publisher;

        public RenderDataPublisherDebugSource(RenderDataPublisher publisher, string name = "Rendering")
        {
            _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
            Name = string.IsNullOrWhiteSpace(name) ? "Rendering" : name;
        }

        public string Name { get; }
        public FrameworkDebugMode Mode => FrameworkDebugMode.Runtime;
        public bool IsAvailable => true;

        public FrameworkDebugSnapshot CreateSnapshot()
        {
            return new FrameworkDebugSnapshot(
                Name,
                Mode,
                new[]
                {
                    new FrameworkDebugSection(RenderingDebugSectionNames.PublisherCounts, Format(_publisher.CaptureSnapshot()))
                });
        }

        private static string Format(RenderDataPublisherSnapshot snapshot)
        {
            var builder = new StringBuilder();
            builder.Append("currentFrameEvents: ").Append(snapshot.CurrentFrameEventCount).Append('\n');
            builder.Append("recentEvents: ").Append(snapshot.RecentEventCount).Append('\n');
            builder.Append("totalEvents: ").Append(snapshot.TotalEventCount);

            for (int i = 0; i < snapshot.TotalCounts.Count; i++)
            {
                RenderDataEventKind kind = snapshot.TotalCounts[i].Kind;
                builder.Append('\n')
                    .Append(kind)
                    .Append(": current=")
                    .Append(snapshot.CurrentFrameCount(kind))
                    .Append(" recent=")
                    .Append(snapshot.RecentCount(kind))
                    .Append(" total=")
                    .Append(snapshot.TotalCount(kind));
            }

            return builder.ToString();
        }
    }
}

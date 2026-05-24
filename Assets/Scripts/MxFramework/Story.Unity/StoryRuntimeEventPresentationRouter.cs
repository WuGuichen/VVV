using System;
using System.Collections.Generic;
using MxFramework.Runtime;
using MxFramework.Story.Runtime;
using UnityEngine;
using UnityEngine.Events;

namespace MxFramework.Story.Unity
{
    [Serializable]
    public sealed class StoryRuntimeEventUnityEvent : UnityEvent<StoryRuntimeEvent>
    {
    }

    [Serializable]
    public sealed class StoryRuntimeEventRoute
    {
        [SerializeField] private StoryEventKind _kind;
        [SerializeField] private StoryRuntimeEventUnityEvent _event = new StoryRuntimeEventUnityEvent();

        public StoryRuntimeEventRoute()
        {
        }

        public StoryRuntimeEventRoute(StoryEventKind kind)
        {
            _kind = kind;
        }

        public StoryEventKind Kind
        {
            get => _kind;
            set => _kind = value;
        }

        public StoryRuntimeEventUnityEvent Event => _event;

        public bool Matches(in StoryRuntimeEvent evt)
        {
            return evt.Kind == _kind;
        }
    }

    public readonly struct StoryRuntimeEventRouteResult
    {
        public StoryRuntimeEventRouteResult(bool supported, bool routed, StoryRuntimeEvent evt, string message)
        {
            Supported = supported;
            Routed = routed;
            Event = evt;
            Message = message ?? string.Empty;
        }

        public bool Supported { get; }
        public bool Routed { get; }
        public StoryRuntimeEvent Event { get; }
        public string Message { get; }
    }

    public static class StoryRuntimePresentationEventPolicy
    {
        public static bool IsPresentationEvent(StoryEventKind kind)
        {
            switch (kind)
            {
                case StoryEventKind.GraphCompleted:
                case StoryEventKind.GraphAborted:
                case StoryEventKind.BeatEntered:
                case StoryEventKind.BeatExited:
                case StoryEventKind.StepStarted:
                case StoryEventKind.StepCompleted:
                case StoryEventKind.ChoiceOffered:
                case StoryEventKind.ChoiceResolved:
                    return true;
                default:
                    return false;
            }
        }
    }

    [DisallowMultipleComponent]
    public sealed class StoryRuntimeEventPresentationRouter : MonoBehaviour
    {
        [SerializeField] private StoryRuntimeEventUnityEvent _onPresentationEvent = new StoryRuntimeEventUnityEvent();
        [SerializeField] private List<StoryRuntimeEventRoute> _routes = new List<StoryRuntimeEventRoute>();
        [SerializeField] private int _maxRecentEvents = 32;

        private readonly List<StoryRuntimeEvent> _recentEvents = new List<StoryRuntimeEvent>();
        private readonly List<StoryRuntimeEvent> _drainBuffer = new List<StoryRuntimeEvent>();

        public StoryRuntimeEventUnityEvent OnPresentationEvent => _onPresentationEvent;
        public IReadOnlyList<StoryRuntimeEventRoute> Routes => _routes;
        public IReadOnlyList<StoryRuntimeEvent> RecentEvents => _recentEvents;
        public StoryRuntimeEventRouteResult LastResult { get; private set; }

        public StoryRuntimeEventRoute AddRoute(StoryEventKind kind)
        {
            var route = new StoryRuntimeEventRoute(kind);
            _routes.Add(route);
            return route;
        }

        public void ClearRoutes()
        {
            _routes.Clear();
        }

        public StoryRuntimeEventRouteResult Route(in StoryRuntimeEvent evt)
        {
            if (!StoryRuntimePresentationEventPolicy.IsPresentationEvent(evt.Kind))
            {
                LastResult = new StoryRuntimeEventRouteResult(false, false, evt, "ignored unsupported Story runtime event kind");
                return LastResult;
            }

            RecordRecent(evt);
            _onPresentationEvent.Invoke(evt);
            for (int i = 0; i < _routes.Count; i++)
            {
                StoryRuntimeEventRoute route = _routes[i];
                if (route != null && route.Matches(evt))
                {
                    route.Event.Invoke(evt);
                }
            }

            LastResult = new StoryRuntimeEventRouteResult(true, true, evt, "routed");
            return LastResult;
        }

        public int Route(IReadOnlyList<StoryRuntimeEvent> events)
        {
            if (events == null)
            {
                return 0;
            }

            int routed = 0;
            for (int i = 0; i < events.Count; i++)
            {
                if (Route(events[i]).Routed)
                {
                    routed++;
                }
            }

            return routed;
        }

        public int DrainAndRoute(RuntimeEventQueue<StoryRuntimeEvent> queue, RuntimeFrame frame)
        {
            if (queue == null)
            {
                throw new ArgumentNullException(nameof(queue));
            }

            _drainBuffer.Clear();
            queue.Drain(frame, _drainBuffer);
            return Route(_drainBuffer);
        }

        private void RecordRecent(in StoryRuntimeEvent evt)
        {
            int max = _maxRecentEvents < 0 ? 0 : _maxRecentEvents;
            if (max == 0)
            {
                _recentEvents.Clear();
                return;
            }

            _recentEvents.Add(evt);
            while (_recentEvents.Count > max)
            {
                _recentEvents.RemoveAt(0);
            }
        }
    }
}

using System;
using System.Collections.Generic;

namespace MxFramework.Combat.Animation
{
    public sealed class CombatActionTimeline
    {
        private readonly CombatActionWindow[] _windows;
        private readonly CombatActionFrameEvent[] _events;

        public CombatActionTimeline(
            int actionId,
            int totalFrames,
            CombatFrameRange startup,
            CombatFrameRange active,
            CombatFrameRange recovery,
            CombatActionWindow[] windows,
            CombatActionFrameEvent[] events)
        {
            if (actionId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(actionId), "Action id must be positive.");
            }

            if (totalFrames <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(totalFrames), "Action total frames must be positive.");
            }

            startup.ValidateWithin(totalFrames, nameof(startup));
            active.ValidateWithin(totalFrames, nameof(active));
            recovery.ValidateWithin(totalFrames, nameof(recovery));

            ActionId = actionId;
            TotalFrames = totalFrames;
            Startup = startup;
            Active = active;
            Recovery = recovery;
            _windows = windows == null ? Array.Empty<CombatActionWindow>() : (CombatActionWindow[])windows.Clone();
            _events = events == null ? Array.Empty<CombatActionFrameEvent>() : (CombatActionFrameEvent[])events.Clone();

            ValidateWindows();
            ValidateEvents();
            Array.Sort(_events);
        }

        public int ActionId { get; }

        public int TotalFrames { get; }

        public CombatFrameRange Startup { get; }

        public CombatFrameRange Active { get; }

        public CombatFrameRange Recovery { get; }

        public int WindowCount => _windows.Length;

        public int EventCount => _events.Length;

        public CombatActionWindow GetWindow(int index)
        {
            if (index < 0 || index >= _windows.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index), "Window index is out of range.");
            }

            return _windows[index];
        }

        public CombatActionPhase GetPhase(int localFrame)
        {
            if (localFrame < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(localFrame), "Local frame cannot be negative.");
            }

            if (localFrame >= TotalFrames)
            {
                return CombatActionPhase.Finished;
            }

            if (Startup.Contains(localFrame))
            {
                return CombatActionPhase.Startup;
            }

            if (Active.Contains(localFrame))
            {
                return CombatActionPhase.Active;
            }

            if (Recovery.Contains(localFrame))
            {
                return CombatActionPhase.Recovery;
            }

            return CombatActionPhase.None;
        }

        public bool IsInWindow(CombatActionWindowKind kind, int localFrame)
        {
            if (localFrame < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(localFrame), "Local frame cannot be negative.");
            }

            for (int i = 0; i < _windows.Length; i++)
            {
                CombatActionWindow window = _windows[i];
                if (window.Kind == kind && window.Contains(localFrame))
                {
                    return true;
                }
            }

            return false;
        }

        public int CollectWindows(CombatActionWindowKind kind, int localFrame, List<CombatActionWindow> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            if (localFrame < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(localFrame), "Local frame cannot be negative.");
            }

            int startCount = results.Count;
            for (int i = 0; i < _windows.Length; i++)
            {
                CombatActionWindow window = _windows[i];
                if (window.Kind == kind && window.Contains(localFrame))
                {
                    results.Add(window);
                }
            }

            return results.Count - startCount;
        }

        public int CollectEvents(int localFrame, List<CombatActionFrameEvent> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            if (localFrame < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(localFrame), "Local frame cannot be negative.");
            }

            int startCount = results.Count;
            for (int i = 0; i < _events.Length; i++)
            {
                CombatActionFrameEvent frameEvent = _events[i];
                if (frameEvent.Frame == localFrame)
                {
                    results.Add(frameEvent);
                }
                else if (frameEvent.Frame > localFrame)
                {
                    break;
                }
            }

            return results.Count - startCount;
        }

        private void ValidateWindows()
        {
            for (int i = 0; i < _windows.Length; i++)
            {
                _windows[i].Range.ValidateWithin(TotalFrames, nameof(_windows));
            }
        }

        private void ValidateEvents()
        {
            for (int i = 0; i < _events.Length; i++)
            {
                if (_events[i].Frame >= TotalFrames)
                {
                    throw new ArgumentOutOfRangeException(nameof(_events), "Frame event must be within action total frames.");
                }
            }
        }
    }
}

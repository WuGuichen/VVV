using System;
using System.Collections.Generic;

namespace MxFramework.Combat.Animation
{
    public sealed class CombatActionRegistry
    {
        private readonly Dictionary<int, CombatActionTimeline> _timelines = new Dictionary<int, CombatActionTimeline>();

        public void RegisterTimeline(int actionId, CombatActionTimeline timeline)
        {
            if (actionId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(actionId), "Action id must be positive.");
            }

            if (timeline == null)
            {
                throw new ArgumentNullException(nameof(timeline));
            }

            if (timeline.ActionId != actionId)
            {
                throw new ArgumentException("Timeline action id must match the registered action id.", nameof(timeline));
            }

            _timelines[actionId] = timeline;
        }

        public bool TryGetTimeline(int actionId, out CombatActionTimeline timeline)
        {
            return _timelines.TryGetValue(actionId, out timeline);
        }

        public bool UnregisterTimeline(int actionId)
        {
            return _timelines.Remove(actionId);
        }
    }
}

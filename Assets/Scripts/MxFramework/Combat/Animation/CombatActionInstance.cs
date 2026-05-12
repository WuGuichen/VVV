using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;

namespace MxFramework.Combat.Animation
{
    public sealed class CombatActionInstance
    {
        public CombatActionInstance(CombatEntityId entityId, CombatActionTimeline timeline, CombatFrame startedAtFrame)
        {
            EntityId = entityId;
            Timeline = timeline ?? throw new ArgumentNullException(nameof(timeline));
            StartedAtFrame = startedAtFrame;
        }

        public CombatEntityId EntityId { get; }

        public CombatActionTimeline Timeline { get; }

        public CombatFrame StartedAtFrame { get; }

        public CombatActionState GetState(CombatFrame worldFrame)
        {
            int localFrame = GetLocalFrame(worldFrame);
            return new CombatActionState(
                EntityId,
                Timeline.ActionId,
                localFrame,
                StartedAtFrame,
                Timeline.GetPhase(localFrame));
        }

        public int CollectEvents(CombatFrame worldFrame, List<CombatActionFrameEvent> results)
        {
            return Timeline.CollectEvents(GetLocalFrame(worldFrame), results);
        }

        public bool IsInWindow(CombatActionWindowKind kind, CombatFrame worldFrame)
        {
            return Timeline.IsInWindow(kind, GetLocalFrame(worldFrame));
        }

        private int GetLocalFrame(CombatFrame worldFrame)
        {
            if (worldFrame < StartedAtFrame)
            {
                throw new ArgumentOutOfRangeException(nameof(worldFrame), "World frame cannot be before action start frame.");
            }

            return worldFrame.Value - StartedAtFrame.Value;
        }
    }
}

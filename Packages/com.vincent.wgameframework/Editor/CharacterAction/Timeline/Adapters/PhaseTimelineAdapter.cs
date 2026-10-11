using System;
using MxFramework.CharacterAction;
using MxFramework.Editor.CharacterAction;
using MxFramework.Editor.CharacterAction.Timeline;
using UnityEngine;

namespace MxFramework.Editor.CharacterAction.Timeline.Adapters
{
    internal sealed class PhaseTimelineAdapter : ITimelineItemAdapter
    {
        private readonly CharacterActionJsonDto _action;
        private readonly CharacterActionPhaseJsonDto _phase;
        private readonly int _initialIndex;
        private readonly int _durationFrames;
        private readonly string _id;

        public PhaseTimelineAdapter(CharacterActionJsonDto action, int index, int durationFrames, bool canEdit)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
            _initialIndex = index;
            _durationFrames = Math.Max(1, durationFrames);
            CharacterActionPhaseJsonDto[] phases = action.Phases ?? Array.Empty<CharacterActionPhaseJsonDto>();
            _phase = index >= 0 && index < phases.Length ? phases[index] : null;
            _id = CharacterActionTimelineIdentity.PhaseId(_phase, index);
            CanEdit = canEdit;
        }

        public string Id => _id;
        public TimelineTrackKind TrackKind => TimelineTrackKind.Phases;
        public TimelineItemKind ItemKind => TimelineItemKind.Clip;
        public bool CanEdit { get; }

        public int StartFrame => GetPhase().StartFrame;
        public int EndFrame => GetPhase().EndFrame;

        public string Title
        {
            get
            {
                return CharacterActionAuthoringText.PhaseSummary(GetPhase());
            }
        }

        public string FullTitle => Title;

        public Color Color => PhaseColor(GetPhase().Kind);

        public bool TryGetCurrentIndex(out int index)
        {
            CharacterActionPhaseJsonDto[] phases = _action.Phases ?? Array.Empty<CharacterActionPhaseJsonDto>();
            if (_phase != null)
            {
                for (int i = 0; i < phases.Length; i++)
                {
                    if (ReferenceEquals(phases[i], _phase))
                    {
                        index = i;
                        return true;
                    }
                }
            }
            else if (_initialIndex >= 0 && _initialIndex < phases.Length && phases[_initialIndex] == null)
            {
                index = _initialIndex;
                return true;
            }

            index = -1;
            return false;
        }

        public void SetRange(int startFrame, int endFrame)
        {
            if (!CanEdit)
                return;

            CharacterActionPhaseTimelineUtility.SetPhaseRange(
                _action,
                _phase,
                startFrame,
                endFrame,
                _durationFrames);
        }

        public void Remove()
        {
            if (!CanEdit)
                return;

            if (TryGetCurrentIndex(out int index))
                CharacterActionPhaseTimelineUtility.RemovePhaseAt(_action, index, _durationFrames);
        }

        private CharacterActionPhaseJsonDto GetPhase()
        {
            CharacterActionPhaseJsonDto[] phases = _action.Phases ?? Array.Empty<CharacterActionPhaseJsonDto>();
            if (!TryGetCurrentIndex(out int index) || index < 0 || index >= phases.Length)
                return new CharacterActionPhaseJsonDto();
            return phases[index] ?? new CharacterActionPhaseJsonDto();
        }

        internal static Color PhaseColor(string kind)
        {
            if (Enum.TryParse(kind, out CharacterActionPhaseKind parsed))
            {
                switch (parsed)
                {
                    case CharacterActionPhaseKind.Startup:
                        return new Color(0.35f, 0.55f, 0.85f, 0.85f);
                    case CharacterActionPhaseKind.Active:
                        return new Color(0.30f, 0.72f, 0.45f, 0.85f);
                    case CharacterActionPhaseKind.Recovery:
                        return new Color(0.85f, 0.62f, 0.28f, 0.85f);
                    case CharacterActionPhaseKind.Airborne:
                        return new Color(0.55f, 0.45f, 0.85f, 0.85f);
                    default:
                        return new Color(0.45f, 0.50f, 0.55f, 0.85f);
                }
            }

            return new Color(0.45f, 0.50f, 0.55f, 0.85f);
        }
    }
}

using MxFramework.Editor.Authoring;
using MxFramework.Editor.CharacterAction.Timeline;

namespace MxFramework.Editor.CharacterAction.Timeline.Preview
{
    internal sealed class CharacterActionTimelinePlaybackController : AuthoringTimelinePlaybackController, ICharacterActionTimelinePlayback
    {
        public CharacterActionTimelinePlaybackController()
            : base(CharacterActionTimelineLayout.EditorTimelineFps)
        {
        }
    }
}

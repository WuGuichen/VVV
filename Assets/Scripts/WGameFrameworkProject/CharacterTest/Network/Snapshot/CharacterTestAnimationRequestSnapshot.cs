using MxFramework.CharacterAction;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    public sealed class CharacterTestAnimationRequestSnapshot
    {
        public int Status { get; set; }
        public long CreatedFrame { get; set; } = -1;
        public long ExpiresFrame { get; set; } = -1;
        public long EmittedFrame { get; set; } = -1;
        public int ActionId { get; set; }
        public int LocalFrame { get; set; } = -1;
        public int BindingId { get; set; }
        public int AuthorityBindingId { get; set; }
        public int StableEventId { get; set; }
        public int TransitionFrames { get; set; }
        public int HoldFrames { get; set; }
        public int PlaybackSpeedMilli { get; set; } = CharacterTestDeterministicUnits.ScaleFactorScale;

        internal bool IsValid()
        {
            if (Status == 0) return BindingId == 0 && AuthorityBindingId == 0 && CreatedFrame == -1 && ExpiresFrame == -1 && EmittedFrame == -1
                && ActionId == 0 && LocalFrame == -1 && StableEventId == 0 && TransitionFrames == 0 && HoldFrames == 0
                && PlaybackSpeedMilli == CharacterTestDeterministicUnits.ScaleFactorScale;
            return Status >= 1 && Status <= 3 && CreatedFrame >= 0 && ExpiresFrame >= CreatedFrame
                && (Status == 1 ? EmittedFrame == -1 : EmittedFrame >= CreatedFrame)
                && ActionId > 0 && LocalFrame >= 0 && TransitionFrames >= 0 && HoldFrames >= 0 && PlaybackSpeedMilli > 0
                && (AuthorityBindingId == 0 || AuthorityBindingId == BindingId)
                && CharacterTestAuthoredContentIds.IsActive(400, BindingId)
                && CharacterTestAuthoredContentIds.IsActive(401, StableEventId);
        }

        internal static CharacterTestAnimationRequestSnapshot Capture(CharacterTestCharacterAnimationRequestComponent request)
        {
            return new CharacterTestAnimationRequestSnapshot
            {
                Status = (int)request.Status, CreatedFrame = request.CreatedFrame, ExpiresFrame = request.ExpiresFrame,
                EmittedFrame = request.EmittedFrame, ActionId = request.ActionId.Value, LocalFrame = request.LocalFrame,
                BindingId = request.BindingId, AuthorityBindingId = request.AuthorityBindingId, StableEventId = request.StableEventId, TransitionFrames = request.TransitionFrames,
                HoldFrames = request.HoldFrames, PlaybackSpeedMilli = request.PlaybackSpeedMilli
            };
        }

        internal CharacterTestCharacterAnimationRequestComponent Restore(string actionKey, string clipResourceKey)
        {
            return CharacterTestCharacterAnimationRequestComponent.Restore((CharacterTestCharacterAnimationRequestStatus)Status,
                CreatedFrame, ExpiresFrame, EmittedFrame, new CharacterActionId(ActionId), LocalFrame, BindingId,
                actionKey, clipResourceKey, TransitionFrames, HoldFrames, PlaybackSpeedMilli, StableEventId, AuthorityBindingId);
        }

        internal CharacterTestAnimationRequestSnapshot Clone() => (CharacterTestAnimationRequestSnapshot)MemberwiseClone();
    }
}

using MxFramework.Runtime;
using UnityEngine;
using UnityCamera = UnityEngine.Camera;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestRuntimeCharacterView
    {
        private bool _clothLodPaused;
        internal long PublishedPoseVersion { get; private set; }
        internal int PublishedPoseFrame { get; private set; } = -1;
        internal long PresentationStateAdvances { get; private set; }
        internal long HeldPoseFrames { get; private set; }
        internal bool HasReliablePoseDeformationVersion => !HasPresentationCloth && !_animationPaused
            && _locomotionController != null && _locomotionController.CanDeferPoseEvaluation
            && (_proceduralBoneRig == null || !_proceduralBoneRig.HasActivePresentationWork);

        private void TickPresentationWithPoseBudget(float deltaTime, RuntimeFrame frame, UnityCamera camera)
        {
            if (Instance == null) return;
            _presentationLod?.RefreshTransform(_presentationLodHandle, EntityId.Value);
            float safeDelta = Mathf.Max(0f, deltaTime);
            bool advancedWithoutPose = false;
            if (!_animationPaused && _presentationLod != null && _locomotionController != null
                && _locomotionController.CanDeferPoseEvaluation)
            {
                if (_actionPlaybackTimeSeconds >= 0d)
                    _locomotionController.TrySynchronizeActionPlaybackTime(_actionPlaybackTimeSeconds);
                advancedWithoutPose = _locomotionController.TryAdvanceRuntimeAnimationState(safeDelta, ToPresentationFrame(frame));
            }
            bool consumerNeedsPose = HasPresentationCloth && !_clothLodPaused
                || _proceduralBoneRig != null && _proceduralBoneRig.HasActivePresentationWork;
            bool evaluate = !advancedWithoutPose || !_locomotionController.CanDeferPoseEvaluation
                || _presentationLod.ShouldEvaluatePose(_presentationLodHandle, EntityId.Value, consumerNeedsPose);
            if (!_animationPaused) PresentationStateAdvances++;
            if (evaluate)
            {
                ClearAuthorityPoseMirrorOffsets();
                RestoreResolvedAuthorityPoseOverrides();
                if (advancedWithoutPose) _locomotionController.EvaluateCurrentAnimationPose();
                else if (!_animationPaused) TickAnimationAtActionTime(safeDelta, frame);
                _boneSync?.ApplyBindings();
                bool movedBones = ApplyResolvedAuthorityPose(frame) | ApplyAuthorityPoseBoneMirror();
                if (movedBones) _boneSync?.ApplyBindings();
                ApplyAuthorityPoseSocketMirror();
                if (_proceduralBoneRig != null && _proceduralBoneRig.HasActivePresentationWork)
                    _proceduralBoneRig.Tick(safeDelta);
                PublishedPoseVersion++;
                PublishedPoseFrame = Time.frameCount;
                _presentationLod?.RefreshPose(_presentationLodHandle, EntityId.Value);
            }
            else HeldPoseFrames++;
            // Preserve critical UI/fade and authoritative root synchronization on every presentation invocation.
            _headPad?.TickPresentation(safeDelta, camera);
        }
    }
}

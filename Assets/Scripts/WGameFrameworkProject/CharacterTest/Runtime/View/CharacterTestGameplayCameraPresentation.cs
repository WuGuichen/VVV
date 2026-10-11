using MxFramework.CharacterApplication;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal readonly struct CharacterTestGameplayCameraSettings
    {
        public CharacterTestGameplayCameraSettings(
            bool explicitProfile,
            string profileId,
            string focusAbstractBoneId,
            Vector3 focusOffset,
            Vector3 framingBoundsCenter,
            Vector3 framingBoundsExtents,
            float distance,
            float minDistance,
            float maxDistance,
            float fieldOfView,
            float pitch,
            float yawOffset,
            float targetPadding,
            float lockedMinDistance,
            float lockedMaxDistance,
            float lockedTargetScreenY,
            float lockedTargetPadding,
            float obstructionRadius,
            float obstructionPadding,
            float obstructionMinDistance,
            float obstructionFocusClearance,
            float lockedFramingScale = 1f)
        {
            ExplicitProfile = explicitProfile;
            ProfileId = profileId ?? string.Empty;
            FocusAbstractBoneId = focusAbstractBoneId ?? string.Empty;
            FocusOffset = focusOffset;
            FramingBoundsCenter = framingBoundsCenter;
            FramingBoundsExtents = framingBoundsExtents;
            Distance = distance;
            MinDistance = minDistance;
            MaxDistance = maxDistance;
            FieldOfView = fieldOfView;
            Pitch = pitch;
            YawOffset = yawOffset;
            TargetPadding = targetPadding;
            LockedMinDistance = lockedMinDistance;
            LockedMaxDistance = lockedMaxDistance;
            LockedTargetScreenY = lockedTargetScreenY;
            LockedTargetPadding = lockedTargetPadding;
            LockedFramingScale = lockedFramingScale;
            ObstructionRadius = obstructionRadius;
            ObstructionPadding = obstructionPadding;
            ObstructionMinDistance = obstructionMinDistance;
            ObstructionFocusClearance = obstructionFocusClearance;
        }

        public bool ExplicitProfile { get; }
        public string ProfileId { get; }
        public string FocusAbstractBoneId { get; }
        public Vector3 FocusOffset { get; }
        public Vector3 FramingBoundsCenter { get; }
        public Vector3 FramingBoundsExtents { get; }
        public float Distance { get; }
        public float MinDistance { get; }
        public float MaxDistance { get; }
        public float FieldOfView { get; }
        public float Pitch { get; }
        public float YawOffset { get; }
        public float TargetPadding { get; }
        public float LockedMinDistance { get; }
        public float LockedMaxDistance { get; }
        public float LockedTargetScreenY { get; }
        public float LockedTargetPadding { get; }
        public float LockedFramingScale { get; }
        public float ObstructionRadius { get; }
        public float ObstructionPadding { get; }
        public float ObstructionMinDistance { get; }
        public float ObstructionFocusClearance { get; }

        public static CharacterTestGameplayCameraSettings FromProfile(
            CharacterGameplayCameraProfile profile)
        {
            return new CharacterTestGameplayCameraSettings(
                true,
                profile.ProfileId,
                profile.FocusAbstractBoneId,
                ToUnity(profile.FocusOffset),
                ToUnity(profile.FramingBoundsCenter),
                ToUnity(profile.FramingBoundsExtents),
                profile.Normal.Distance,
                profile.Normal.MinDistance,
                profile.Normal.MaxDistance,
                profile.Normal.FieldOfView,
                profile.Normal.Pitch,
                profile.Normal.YawOffset,
                profile.Normal.TargetPadding,
                profile.Locked.MinDistance,
                profile.Locked.MaxDistance,
                profile.Locked.TargetScreenY,
                profile.Locked.TargetPadding,
                profile.Obstruction.Radius,
                profile.Obstruction.Padding,
                profile.Obstruction.MinDistance,
                profile.Obstruction.FocusClearance,
                profile.Locked.FramingScale);
        }

        private static Vector3 ToUnity(CharacterGameplayCameraVector3 value) =>
            new Vector3(value.X, value.Y, value.Z);
    }

    internal readonly struct CharacterTestCameraTargetPresentation
    {
        public CharacterTestCameraTargetPresentation(
            CharacterTestCharacterEntity entity,
            Vector3 focus,
            Vector3 forward,
            Vector3 rootPosition,
            Vector3 boundsCenter,
            Vector3 boundsExtents,
            CharacterTestGameplayCameraSettings settings)
        {
            Entity = entity;
            Focus = focus;
            Forward = forward;
            RootPosition = rootPosition;
            BoundsCenter = boundsCenter;
            BoundsExtents = boundsExtents;
            Settings = settings;
        }

        public CharacterTestCharacterEntity Entity { get; }
        public Vector3 Focus { get; }
        public Vector3 Forward { get; }
        public Vector3 RootPosition { get; }
        public Vector3 BoundsCenter { get; }
        public Vector3 BoundsExtents { get; }
        public CharacterTestGameplayCameraSettings Settings { get; }
    }
}

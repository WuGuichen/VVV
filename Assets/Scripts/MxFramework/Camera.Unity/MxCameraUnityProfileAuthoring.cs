using System.Collections.Generic;
using UnityEngine;

namespace MxFramework.Camera.Unity
{
    [System.Serializable]
    public sealed class MxCameraUnityProfileDefinition
    {
        public string profileId = "default";
        public MxCameraMode mode = MxCameraMode.GroupFollowPerspective;
        public int priority;
        public Vector3 localOffset = Vector3.zero;
        public Vector3 worldOffset = Vector3.zero;
        public float distance = 8f;
        public float minDistance = 2f;
        public float maxDistance = 18f;
        public float fieldOfView = 60f;
        public float minFieldOfView = 20f;
        public float maxFieldOfView = 80f;
        public float orthographicSize = 6f;
        public float minOrthographicSize = 2f;
        public float maxOrthographicSize = 24f;
        public float positionSmoothing;
        public float rotationSmoothing;
        public float zoomSmoothing;
        public float targetPadding = 0.5f;
        public int targetLostGraceFrames = 6;
        public MxCameraBoundsPolicy boundsPolicy;
        public float maxTargetRadius = 64f;
        public float shakeLimit = 1f;
        public float pitch = 35f;
        public float yaw;
        public Vector3 fallbackPosition = new Vector3(0f, 6f, -8f);
        public Vector3 fallbackFocus = Vector3.zero;
        public string[] diagnosticTags = new string[0];

        public MxCameraProfileDefinition ToRuntimeDefinition()
        {
            return new MxCameraProfileDefinition
            {
                ProfileId = new MxCameraProfileId(profileId),
                Mode = mode,
                Priority = priority,
                LocalOffset = MxCameraUnityConversions.ToCameraVector(localOffset),
                WorldOffset = MxCameraUnityConversions.ToCameraVector(worldOffset),
                Distance = distance,
                MinDistance = minDistance,
                MaxDistance = maxDistance,
                FieldOfView = fieldOfView,
                MinFieldOfView = minFieldOfView,
                MaxFieldOfView = maxFieldOfView,
                OrthographicSize = orthographicSize,
                MinOrthographicSize = minOrthographicSize,
                MaxOrthographicSize = maxOrthographicSize,
                PositionSmoothing = positionSmoothing,
                RotationSmoothing = rotationSmoothing,
                ZoomSmoothing = zoomSmoothing,
                TargetPadding = targetPadding,
                TargetLostGraceFrames = targetLostGraceFrames,
                BoundsPolicy = boundsPolicy,
                MaxTargetRadius = maxTargetRadius,
                ShakeLimit = shakeLimit,
                Pitch = pitch,
                Yaw = yaw,
                FallbackPosition = MxCameraUnityConversions.ToCameraVector(fallbackPosition),
                FallbackFocus = MxCameraUnityConversions.ToCameraVector(fallbackFocus),
                DiagnosticTags = diagnosticTags ?? new string[0]
            };
        }
    }

    [CreateAssetMenu(fileName = "MxCameraProfile", menuName = "MxFramework/Camera/Profile")]
    public sealed class MxCameraProfileAuthoringAsset : ScriptableObject
    {
        [SerializeField] private MxCameraUnityProfileDefinition _profile = new MxCameraUnityProfileDefinition();

        public MxCameraUnityProfileDefinition Profile => _profile;

        public MxCameraProfileDefinition ExportRuntimeDefinition()
        {
            return (_profile ?? new MxCameraUnityProfileDefinition()).ToRuntimeDefinition();
        }

        public IReadOnlyList<MxCameraDiagnostic> Validate()
        {
            return new MxCameraProfileValidator().Validate(ExportRuntimeDefinition());
        }
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("MxFramework/Camera/Camera Profile Provider")]
    public sealed class MxCameraUnityProfileProvider : MonoBehaviour, IMxCameraProfileProvider
    {
        [SerializeField] private MxCameraProfileAuthoringAsset[] _profiles;
        [SerializeField] private MxCameraUnityProfileDefinition _fallbackProfile = new MxCameraUnityProfileDefinition();

        private readonly List<MxCameraProfileDefinition> _runtimeProfiles = new List<MxCameraProfileDefinition>();

        public IReadOnlyList<MxCameraProfileDefinition> Profiles
        {
            get
            {
                Rebuild();
                return _runtimeProfiles;
            }
        }

        private void Rebuild()
        {
            _runtimeProfiles.Clear();
            if (_profiles != null)
            {
                for (int i = 0; i < _profiles.Length; i++)
                {
                    if (_profiles[i] != null)
                        _runtimeProfiles.Add(_profiles[i].ExportRuntimeDefinition());
                }
            }

            if (_runtimeProfiles.Count == 0)
                _runtimeProfiles.Add((_fallbackProfile ?? new MxCameraUnityProfileDefinition()).ToRuntimeDefinition());
        }
    }
}

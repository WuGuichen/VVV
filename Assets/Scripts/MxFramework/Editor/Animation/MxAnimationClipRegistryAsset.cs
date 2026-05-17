using System;
using MxFramework.Animation;
using MxFramework.Resources;
using UnityEngine;

namespace MxFramework.Editor.Animation
{
    [CreateAssetMenu(
        fileName = "MxAnimationClipRegistry",
        menuName = "MxFramework/Animation/Clip Registry")]
    public sealed class MxAnimationClipRegistryAsset : ScriptableObject
    {
        [SerializeField] private string animationSetId = "animation.set";
        [SerializeField] private int version = 1;
        [SerializeField] private string packageId = string.Empty;
        [SerializeField] private MxAnimationClipRegistryClipEntry[] clips = Array.Empty<MxAnimationClipRegistryClipEntry>();
        [SerializeField] private MxAnimationClipRegistryLayerEntry[] layers = Array.Empty<MxAnimationClipRegistryLayerEntry>();
        [SerializeField] private MxAnimationClipRegistryBindingEntry[] bindings = Array.Empty<MxAnimationClipRegistryBindingEntry>();
        [SerializeField] private MxAnimationClipRegistryBlend1DEntry[] blend1DDefinitions = Array.Empty<MxAnimationClipRegistryBlend1DEntry>();
        [SerializeField] private MxAnimationClipRegistryBlend2DEntry[] blend2DDefinitions = Array.Empty<MxAnimationClipRegistryBlend2DEntry>();
        [SerializeField] private MxAnimationClipRegistryEventEntry[] events = Array.Empty<MxAnimationClipRegistryEventEntry>();

        public string AnimationSetId
        {
            get => animationSetId;
            set => animationSetId = value ?? string.Empty;
        }

        public int Version
        {
            get => version;
            set => version = value;
        }

        public string PackageId
        {
            get => packageId;
            set => packageId = value ?? string.Empty;
        }

        public MxAnimationClipRegistryClipEntry[] Clips
        {
            get => clips;
            set => clips = value ?? Array.Empty<MxAnimationClipRegistryClipEntry>();
        }

        public MxAnimationClipRegistryLayerEntry[] Layers
        {
            get => layers;
            set => layers = value ?? Array.Empty<MxAnimationClipRegistryLayerEntry>();
        }

        public MxAnimationClipRegistryBindingEntry[] Bindings
        {
            get => bindings;
            set => bindings = value ?? Array.Empty<MxAnimationClipRegistryBindingEntry>();
        }

        public MxAnimationClipRegistryBlend1DEntry[] Blend1DDefinitions
        {
            get => blend1DDefinitions;
            set => blend1DDefinitions = value ?? Array.Empty<MxAnimationClipRegistryBlend1DEntry>();
        }

        public MxAnimationClipRegistryBlend2DEntry[] Blend2DDefinitions
        {
            get => blend2DDefinitions;
            set => blend2DDefinitions = value ?? Array.Empty<MxAnimationClipRegistryBlend2DEntry>();
        }

        public MxAnimationClipRegistryEventEntry[] Events
        {
            get => events;
            set => events = value ?? Array.Empty<MxAnimationClipRegistryEventEntry>();
        }
    }

    [Serializable]
    public struct MxAnimationClipRegistryLayerEntry
    {
        [SerializeField] private string layerId;
        [SerializeField] private string profileId;
        [SerializeField] private float defaultWeight;
        [SerializeField] private MxAnimationLayerBlendMode blendMode;
        [SerializeField] private AvatarMask avatarMask;
        [SerializeField] private string avatarMaskResourceId;
        [SerializeField] private string avatarMaskVariant;
        [SerializeField] private string avatarMaskPackageId;

        public string LayerId
        {
            get => layerId;
            set => layerId = value ?? string.Empty;
        }

        public string ProfileId
        {
            get => profileId;
            set => profileId = value ?? string.Empty;
        }

        public float DefaultWeight
        {
            get
            {
                if (float.IsNaN(defaultWeight) || defaultWeight <= 0f)
                    return 0f;
                return defaultWeight >= 1f ? 1f : defaultWeight;
            }
            set => defaultWeight = value;
        }

        public MxAnimationLayerBlendMode BlendMode
        {
            get => blendMode;
            set => blendMode = value;
        }

        public AvatarMask AvatarMask
        {
            get => avatarMask;
            set => avatarMask = value;
        }

        public string AvatarMaskResourceId
        {
            get => avatarMaskResourceId;
            set => avatarMaskResourceId = value ?? string.Empty;
        }

        public string AvatarMaskVariant
        {
            get => avatarMaskVariant;
            set => avatarMaskVariant = value ?? string.Empty;
        }

        public string AvatarMaskPackageId
        {
            get => avatarMaskPackageId;
            set => avatarMaskPackageId = value ?? string.Empty;
        }

        public ResourceKey CreateAvatarMaskKey(string fallbackPackageId)
        {
            if (string.IsNullOrWhiteSpace(avatarMaskResourceId))
                return default;

            string resolvedPackageId = string.IsNullOrWhiteSpace(avatarMaskPackageId) ? fallbackPackageId : avatarMaskPackageId;
            return new ResourceKey(avatarMaskResourceId, ResourceTypeIds.AvatarMask, avatarMaskVariant, resolvedPackageId);
        }
    }

    [Serializable]
    public struct MxAnimationClipRegistryClipEntry
    {
        [SerializeField] private string clipId;
        [SerializeField] private AnimationClip clip;
        [SerializeField] private string resourceId;
        [SerializeField] private string variant;
        [SerializeField] private string packageId;
        [SerializeField] private bool isDefault;
        [SerializeField] private bool isFallback;

        public string ClipId
        {
            get => clipId;
            set => clipId = value ?? string.Empty;
        }

        public AnimationClip Clip
        {
            get => clip;
            set => clip = value;
        }

        public string ResourceId
        {
            get => resourceId;
            set => resourceId = value ?? string.Empty;
        }

        public string Variant
        {
            get => variant;
            set => variant = value ?? string.Empty;
        }

        public string PackageId
        {
            get => packageId;
            set => packageId = value ?? string.Empty;
        }

        public bool IsDefault
        {
            get => isDefault;
            set => isDefault = value;
        }

        public bool IsFallback
        {
            get => isFallback;
            set => isFallback = value;
        }

        public ResourceKey CreateResourceKey(string fallbackPackageId)
        {
            string resolvedPackageId = string.IsNullOrWhiteSpace(packageId) ? fallbackPackageId : packageId;
            return new ResourceKey(resourceId, ResourceTypeIds.AnimationClip, variant, resolvedPackageId);
        }
    }

    [Serializable]
    public struct MxAnimationClipRegistryBindingEntry
    {
        [SerializeField] private string bindingId;
        [SerializeField] private int actionId;
        [SerializeField] private string actionKey;
        [SerializeField] private string clipId;
        [SerializeField] private string layerId;
        [SerializeField] private float playbackSpeed;
        [SerializeField] private bool loop;
        [SerializeField] private MxAnimationAlignmentPolicy alignmentPolicy;
        [SerializeField] private float fadeDurationSeconds;
        [SerializeField] private MxAnimationClipRegistryEventEntry[] events;

        public string BindingId
        {
            get => bindingId;
            set => bindingId = value ?? string.Empty;
        }

        public int ActionId
        {
            get => actionId;
            set => actionId = value;
        }

        public string ActionKey
        {
            get => actionKey;
            set => actionKey = value ?? string.Empty;
        }

        public string ClipId
        {
            get => clipId;
            set => clipId = value ?? string.Empty;
        }

        public string LayerId
        {
            get => layerId;
            set => layerId = value ?? string.Empty;
        }

        public float PlaybackSpeed
        {
            get => playbackSpeed <= 0f ? 1f : playbackSpeed;
            set => playbackSpeed = value;
        }

        public bool Loop
        {
            get => loop;
            set => loop = value;
        }

        public MxAnimationAlignmentPolicy AlignmentPolicy
        {
            get => alignmentPolicy;
            set => alignmentPolicy = value;
        }

        public float FadeDurationSeconds
        {
            get => fadeDurationSeconds < 0f ? 0f : fadeDurationSeconds;
            set => fadeDurationSeconds = value;
        }

        public MxAnimationClipRegistryEventEntry[] Events
        {
            get => events ?? Array.Empty<MxAnimationClipRegistryEventEntry>();
            set => events = value ?? Array.Empty<MxAnimationClipRegistryEventEntry>();
        }

        public string ResolveActionKey()
        {
            if (!string.IsNullOrWhiteSpace(actionKey))
                return actionKey;

            return actionId > 0 ? "action:" + actionId : string.Empty;
        }
    }

    [Serializable]
    public struct MxAnimationClipRegistryBlend1DEntry
    {
        [SerializeField] private string blendId;
        [SerializeField] private string parameterId;
        [SerializeField] private string layerId;
        [SerializeField] private int parameterScale;
        [SerializeField] private float fadeDurationSeconds;
        [SerializeField] private MxAnimationClipRegistryBlend1DPointEntry[] points;

        public string BlendId
        {
            get => blendId;
            set => blendId = value ?? string.Empty;
        }

        public string ParameterId
        {
            get => parameterId;
            set => parameterId = value ?? string.Empty;
        }

        public string LayerId
        {
            get => layerId;
            set => layerId = value ?? string.Empty;
        }

        public int ParameterScale
        {
            get => parameterScale <= 0 ? 1000 : parameterScale;
            set => parameterScale = value;
        }

        public float FadeDurationSeconds
        {
            get => fadeDurationSeconds < 0f ? 0f : fadeDurationSeconds;
            set => fadeDurationSeconds = value;
        }

        public MxAnimationClipRegistryBlend1DPointEntry[] Points
        {
            get => points ?? Array.Empty<MxAnimationClipRegistryBlend1DPointEntry>();
            set => points = value ?? Array.Empty<MxAnimationClipRegistryBlend1DPointEntry>();
        }
    }

    [Serializable]
    public struct MxAnimationClipRegistryBlend1DPointEntry
    {
        [SerializeField] private int threshold;
        [SerializeField] private string clipId;
        [SerializeField] private float playbackSpeed;
        [SerializeField] private bool loop;

        public int Threshold
        {
            get => threshold;
            set => threshold = value;
        }

        public string ClipId
        {
            get => clipId;
            set => clipId = value ?? string.Empty;
        }

        public float PlaybackSpeed
        {
            get => playbackSpeed <= 0f ? 1f : playbackSpeed;
            set => playbackSpeed = value;
        }

        public bool Loop
        {
            get => loop;
            set => loop = value;
        }
    }

    [Serializable]
    public struct MxAnimationClipRegistryBlend2DEntry
    {
        [SerializeField] private string blendId;
        [SerializeField] private string parameterXId;
        [SerializeField] private string parameterYId;
        [SerializeField] private string layerId;
        [SerializeField] private int parameterXScale;
        [SerializeField] private int parameterYScale;
        [SerializeField] private float fadeDurationSeconds;
        [SerializeField] private MxAnimationClipRegistryBlend2DPointEntry[] points;

        public string BlendId
        {
            get => blendId;
            set => blendId = value ?? string.Empty;
        }

        public string ParameterXId
        {
            get => parameterXId;
            set => parameterXId = value ?? string.Empty;
        }

        public string ParameterYId
        {
            get => parameterYId;
            set => parameterYId = value ?? string.Empty;
        }

        public string LayerId
        {
            get => layerId;
            set => layerId = value ?? string.Empty;
        }

        public int ParameterXScale
        {
            get => parameterXScale <= 0 ? 1000 : parameterXScale;
            set => parameterXScale = value;
        }

        public int ParameterYScale
        {
            get => parameterYScale <= 0 ? 1000 : parameterYScale;
            set => parameterYScale = value;
        }

        public float FadeDurationSeconds
        {
            get => fadeDurationSeconds < 0f ? 0f : fadeDurationSeconds;
            set => fadeDurationSeconds = value;
        }

        public MxAnimationClipRegistryBlend2DPointEntry[] Points
        {
            get => points ?? Array.Empty<MxAnimationClipRegistryBlend2DPointEntry>();
            set => points = value ?? Array.Empty<MxAnimationClipRegistryBlend2DPointEntry>();
        }
    }

    [Serializable]
    public struct MxAnimationClipRegistryBlend2DPointEntry
    {
        [SerializeField] private int x;
        [SerializeField] private int y;
        [SerializeField] private string clipId;
        [SerializeField] private float playbackSpeed;
        [SerializeField] private bool loop;

        public int X
        {
            get => x;
            set => x = value;
        }

        public int Y
        {
            get => y;
            set => y = value;
        }

        public string ClipId
        {
            get => clipId;
            set => clipId = value ?? string.Empty;
        }

        public float PlaybackSpeed
        {
            get => playbackSpeed <= 0f ? 1f : playbackSpeed;
            set => playbackSpeed = value;
        }

        public bool Loop
        {
            get => loop;
            set => loop = value;
        }
    }

    [Serializable]
    public struct MxAnimationClipRegistryEventEntry
    {
        [SerializeField] private string eventId;
        [SerializeField] private MxAnimationEventTimeDomain timeDomain;
        [SerializeField] private float time;
        [SerializeField] private string eventKind;
        [SerializeField] private string payloadResourceId;
        [SerializeField] private string payloadTypeId;
        [SerializeField] private string payloadVariant;
        [SerializeField] private string payloadPackageId;
        [SerializeField] private string socket;
        [SerializeField] private string tag;
        [SerializeField] private MxAnimationPresentationEventReplayPolicy replayPolicy;

        public string EventId
        {
            get => eventId;
            set => eventId = value ?? string.Empty;
        }

        public MxAnimationEventTimeDomain TimeDomain
        {
            get => timeDomain;
            set => timeDomain = value;
        }

        public float Time
        {
            get => time;
            set => time = value;
        }

        public string EventKind
        {
            get => eventKind;
            set => eventKind = value ?? string.Empty;
        }

        public string PayloadResourceId
        {
            get => payloadResourceId;
            set => payloadResourceId = value ?? string.Empty;
        }

        public string PayloadTypeId
        {
            get => payloadTypeId;
            set => payloadTypeId = value ?? string.Empty;
        }

        public string PayloadVariant
        {
            get => payloadVariant;
            set => payloadVariant = value ?? string.Empty;
        }

        public string PayloadPackageId
        {
            get => payloadPackageId;
            set => payloadPackageId = value ?? string.Empty;
        }

        public string Socket
        {
            get => socket;
            set => socket = value ?? string.Empty;
        }

        public string Tag
        {
            get => tag;
            set => tag = value ?? string.Empty;
        }

        public MxAnimationPresentationEventReplayPolicy ReplayPolicy
        {
            get => replayPolicy;
            set => replayPolicy = value;
        }

        public ResourceKey CreatePayloadKey()
        {
            if (string.IsNullOrWhiteSpace(payloadResourceId) || string.IsNullOrWhiteSpace(payloadTypeId))
                return default;

            return new ResourceKey(payloadResourceId, payloadTypeId, payloadVariant, payloadPackageId);
        }
    }
}

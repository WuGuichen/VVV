using System;
using UnityEngine;

namespace MxFramework.Combat.Authoring
{
    [CreateAssetMenu(
        fileName = "CombatSceneBindingAsset",
        menuName = "MxFramework/Combat/Scene Binding Asset")]
    public sealed class CombatSceneBindingAsset : ScriptableObject
    {
        [SerializeField] private string sceneGuid;
        [SerializeField] private string bindingProfileId;
        [SerializeField] private CombatActorBindingData[] actors = Array.Empty<CombatActorBindingData>();
        [SerializeField] private CombatMarkerBindingData[] markers = Array.Empty<CombatMarkerBindingData>();

        public string SceneGuid
        {
            get => sceneGuid;
            set => sceneGuid = value;
        }

        public string BindingProfileId
        {
            get => bindingProfileId;
            set => bindingProfileId = value;
        }

        public CombatActorBindingData[] Actors
        {
            get => actors;
            set => actors = value ?? Array.Empty<CombatActorBindingData>();
        }

        public CombatMarkerBindingData[] Markers
        {
            get => markers;
            set => markers = value ?? Array.Empty<CombatMarkerBindingData>();
        }
    }

    [Serializable]
    public struct CombatActorBindingData
    {
        [SerializeField] private int entityId;
        [SerializeField] private string displayName;
        [SerializeField] private string markerId;
        [SerializeField] private int bodyId;
        [SerializeField] private CombatColliderBindingData[] colliders;

        public int EntityId
        {
            get => entityId;
            set => entityId = value;
        }

        public string DisplayName
        {
            get => displayName;
            set => displayName = value;
        }

        public string MarkerId
        {
            get => markerId;
            set => markerId = value;
        }

        public int BodyId
        {
            get => bodyId;
            set => bodyId = value;
        }

        public CombatColliderBindingData[] Colliders
        {
            get => colliders;
            set => colliders = value ?? Array.Empty<CombatColliderBindingData>();
        }
    }

    [Serializable]
    public struct CombatColliderBindingData
    {
        [SerializeField] private int colliderId;
        [SerializeField] private string markerId;
        [SerializeField] private int sourceOrder;

        public int ColliderId
        {
            get => colliderId;
            set => colliderId = value;
        }

        public string MarkerId
        {
            get => markerId;
            set => markerId = value;
        }

        public int SourceOrder
        {
            get => sourceOrder;
            set => sourceOrder = value;
        }
    }

    [Serializable]
    public struct CombatMarkerBindingData
    {
        [SerializeField] private string markerId;
        [SerializeField] private string targetPath;
        [SerializeField] private int sourceOrder;

        public string MarkerId
        {
            get => markerId;
            set => markerId = value;
        }

        public string TargetPath
        {
            get => targetPath;
            set => targetPath = value;
        }

        public int SourceOrder
        {
            get => sourceOrder;
            set => sourceOrder = value;
        }
    }
}

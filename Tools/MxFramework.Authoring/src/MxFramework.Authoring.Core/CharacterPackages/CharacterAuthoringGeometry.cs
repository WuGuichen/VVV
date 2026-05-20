using System.Collections.Generic;

namespace MxFramework.Authoring
{
    public enum CharacterAuthoringBodyKind
    {
        Unknown = 0,
        Skeletal = 1,
        Primitive = 2,
        Compound = 3
    }

    public enum CharacterAuthoringBodyPartKind
    {
        Unknown = 0,
        Bone = 1,
        Primitive = 2,
        Virtual = 3
    }

    public enum CharacterPoseParentKind
    {
        Unknown = 0,
        ModelRoot = 1,
        SkeletonRoot = 2,
        Bone = 3,
        Locator = 4,
        BodyPart = 5,
        Socket = 6,
        WorldPreview = 7
    }

    public enum CharacterColliderShape
    {
        Unknown = 0,
        Capsule = 1,
        Box = 2,
        Sphere = 3,
        Convex = 100,
        CustomMesh = 101,
        Reserved1000 = 1000,
        Reserved1001 = 1001
    }

    public enum CharacterSocketUsage
    {
        Unknown = 0,
        Weapon = 1,
        Vfx = 2,
        Camera = 3,
        Ui = 4,
        Gameplay = 5
    }

    public enum CharacterSocketHandedness
    {
        Unknown = 0,
        None = 1,
        Left = 2,
        Right = 3,
        Both = 4
    }

    public enum CharacterSocketSideTag
    {
        Unknown = 0,
        Center = 1,
        Left = 2,
        Right = 3,
        Front = 4,
        Back = 5
    }

    public enum WeaponTraceSampleRule
    {
        Unknown = 0,
        LineSegment = 1,
        CapsuleSweep = 2,
        FixedSamples = 3
    }

    public sealed class CharacterAuthoringVector3
    {
        public CharacterAuthoringVector3()
        {
        }

        public CharacterAuthoringVector3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
    }

    public sealed class CharacterAuthoringQuaternion
    {
        public CharacterAuthoringQuaternion()
        {
            W = 1f;
        }

        public CharacterAuthoringQuaternion(float x, float y, float z, float w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float W { get; set; } = 1f;
    }

    public sealed class CharacterAuthoringLocalPose
    {
        public CharacterPoseParentKind ParentKind { get; set; } = CharacterPoseParentKind.ModelRoot;
        public string ParentPath { get; set; } = string.Empty;
        public CharacterAuthoringVector3 Position { get; set; } = new CharacterAuthoringVector3();
        public CharacterAuthoringQuaternion Rotation { get; set; } = new CharacterAuthoringQuaternion();
        public CharacterAuthoringVector3 Scale { get; set; } = new CharacterAuthoringVector3(1f, 1f, 1f);
        public CharacterAuthoringVector3 EulerHint { get; set; } = new CharacterAuthoringVector3();
    }

    public sealed class CharacterCapsuleAuthoring
    {
        public float Height { get; set; }
        public float Radius { get; set; }
        public CharacterAuthoringVector3 Center { get; set; } = new CharacterAuthoringVector3();
    }

    public sealed class CharacterBodyGeometryProfile
    {
        public string ProfileId { get; set; } = string.Empty;
        public CharacterAuthoringBodyKind BodyKind { get; set; } = CharacterAuthoringBodyKind.Unknown;
        public float BodyScale { get; set; } = 1f;
        public float HeightMeters { get; set; }
        public float RadiusMeters { get; set; }
        public float MassKg { get; set; }
        public CharacterCapsuleAuthoring DefaultCapsule { get; set; } = new CharacterCapsuleAuthoring();
        public string DefaultPhysicsProfileId { get; set; } = string.Empty;
        public string ModelRootStableId { get; set; } = string.Empty;
        public string SkeletonRootStableId { get; set; } = string.Empty;
        public string LocatorRootStableId { get; set; } = string.Empty;
    }

    public sealed class CharacterBodyPartAuthoring
    {
        public string PartId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public CharacterAuthoringBodyPartKind PartKind { get; set; } = CharacterAuthoringBodyPartKind.Unknown;
        public string ParentPartId { get; set; } = string.Empty;
        public string BonePath { get; set; } = string.Empty;
        public string LocatorId { get; set; } = string.Empty;
        public string DefaultHitZoneId { get; set; } = string.Empty;
        public string ReactionGroupId { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new List<string>();
    }

    public sealed class CharacterBodyColliderProfile
    {
        public string ColliderId { get; set; } = string.Empty;
        public string PartId { get; set; } = string.Empty;
        public string HitZoneId { get; set; } = string.Empty;
        public CharacterColliderShape Shape { get; set; } = CharacterColliderShape.Unknown;
        public CharacterAuthoringLocalPose LocalPose { get; set; } = new CharacterAuthoringLocalPose();
        public CharacterAuthoringVector3 Size { get; set; } = new CharacterAuthoringVector3();
        public float Radius { get; set; }
        public float Height { get; set; }
        public int Priority { get; set; }
        public bool IsWeakPoint { get; set; }
        public float DamageMultiplierOverride { get; set; }
        public float PostureDamageScaleOverride { get; set; }
        public string PhysicsLayer { get; set; } = string.Empty;
        public string MaterialStableId { get; set; } = string.Empty;
    }

    public sealed class CharacterSocketProfile
    {
        public string SocketId { get; set; } = string.Empty;
        public string ParentPartId { get; set; } = string.Empty;
        public string BonePath { get; set; } = string.Empty;
        public string LocatorPath { get; set; } = string.Empty;
        public CharacterAuthoringLocalPose LocalPose { get; set; } = new CharacterAuthoringLocalPose();
        public CharacterSocketUsage Usage { get; set; } = CharacterSocketUsage.Unknown;
        public string MirrorPairSocketId { get; set; } = string.Empty;
        public CharacterSocketHandedness Handedness { get; set; } = CharacterSocketHandedness.None;
        public CharacterSocketSideTag SideTag { get; set; } = CharacterSocketSideTag.Center;
        public List<string> Tags { get; set; } = new List<string>();
    }

    public sealed class WeaponAttachmentProfile
    {
        public string WeaponId { get; set; } = string.Empty;
        public string EquipSlot { get; set; } = string.Empty;
        public string AttachSocketId { get; set; } = string.Empty;
        public CharacterAuthoringLocalPose LocalGripPose { get; set; } = new CharacterAuthoringLocalPose();
        public string PreviewResourceKey { get; set; } = string.Empty;
        public AuthoringResourceSelectionRef PreviewResourceSelection { get; set; } = new AuthoringResourceSelectionRef();
        public string TraceId { get; set; } = string.Empty;
        public string TraceStartSocketId { get; set; } = string.Empty;
        public string TraceEndSocketId { get; set; } = string.Empty;
        public float TraceRadius { get; set; }
        public WeaponTraceSampleRule TraceSampleRule { get; set; } = WeaponTraceSampleRule.CapsuleSweep;
    }

    public sealed class WeaponTraceProfile
    {
        public string TraceId { get; set; } = string.Empty;
        public string WeaponId { get; set; } = string.Empty;
        public string EquipSlot { get; set; } = string.Empty;
        public string StartLocatorPath { get; set; } = string.Empty;
        public string EndLocatorPath { get; set; } = string.Empty;
        public CharacterAuthoringLocalPose StartPose { get; set; } = new CharacterAuthoringLocalPose();
        public CharacterAuthoringLocalPose EndPose { get; set; } = new CharacterAuthoringLocalPose();
        public float Radius { get; set; }
        public WeaponTraceSampleRule SampleRule { get; set; } = WeaponTraceSampleRule.CapsuleSweep;
        public int FixedSampleCount { get; set; } = 4;
        public List<string> ActionKeys { get; set; } = new List<string>();
    }

    public sealed class CharacterAuthoringGeometry
    {
        public string SchemaVersion { get; set; } = "1.0";
        public CharacterBodyGeometryProfile BodyProfile { get; set; } = new CharacterBodyGeometryProfile();
        public List<CharacterBodyPartAuthoring> BodyParts { get; set; } = new List<CharacterBodyPartAuthoring>();
        public List<CharacterBodyColliderProfile> Colliders { get; set; } = new List<CharacterBodyColliderProfile>();
        public List<CharacterSocketProfile> Sockets { get; set; } = new List<CharacterSocketProfile>();
        public List<WeaponAttachmentProfile> WeaponAttachments { get; set; } = new List<WeaponAttachmentProfile>();
        public List<WeaponTraceProfile> Traces { get; set; } = new List<WeaponTraceProfile>();
    }
}

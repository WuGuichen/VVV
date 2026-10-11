using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MxFramework.Authoring
{
    public static class CharacterAbstractBoneIds
    {
        public const string Root = "root";
        public const string BodyCenter = "body_center";
        public const string Head = "head";
        public const string Mouth = "mouth";
        public const string LeftHand = "left_hand";
        public const string RightHand = "right_hand";
        public const string LeftFoot = "left_foot";
        public const string RightFoot = "right_foot";
        public const string TailBase = "tail_base";
        public const string TailTip = "tail_tip";
        public const string LeftWing = "left_wing";
        public const string RightWing = "right_wing";

        private static readonly HashSet<string> CoreIds = new HashSet<string>(StringComparer.Ordinal)
        {
            Root, BodyCenter, Head, Mouth, LeftHand, RightHand, LeftFoot, RightFoot,
            TailBase, TailTip, LeftWing, RightWing
        };

        public static bool IsCore(string value) => !string.IsNullOrWhiteSpace(value) && CoreIds.Contains(value);

        public static bool IsValid(string packageId, string value)
        {
            if (IsCore(value))
                return true;
            string prefix = "ext." + (packageId ?? string.Empty) + ".";
            if (string.IsNullOrWhiteSpace(packageId) || string.IsNullOrWhiteSpace(value)
                || !value.StartsWith(prefix, StringComparison.Ordinal) || value.Length <= prefix.Length)
                return false;
            for (int i = prefix.Length; i < value.Length; i++)
            {
                char c = value[i];
                if (!char.IsLower(c) && !char.IsDigit(c) && c != '_' && c != '-')
                    return false;
            }
            return true;
        }
    }

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

    public enum CharacterBodyPartContactActivationMode
    {
        Disabled = 0,
        Always = 1,
        ActionWindow = 2,
        ExplicitState = 3,
        MotionState = 4
    }

    public enum CharacterBodyPartContactMotionMode
    {
        Static = 0,
        BakedSocket = 1
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
        public float ImpactPowerScaleOverride { get; set; }
        public string PhysicsLayer { get; set; } = string.Empty;
        public string MaterialStableId { get; set; } = string.Empty;
    }

    public enum CharacterDamageZonesDocumentState
    {
        Missing = 0,
        Current = 1,
        UnsupportedSchema = 2,
        Invalid = 3
    }

    /// <summary>Author-owned grouping of one or more HitZone identities.</summary>
    public sealed class CharacterDamageZoneAuthoring
    {
        public string ZoneId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public List<string> HitZoneIds { get; set; } = new List<string>();
        public bool Hittable { get; set; }
        public bool Aimable { get; set; }
        public bool Approachable { get; set; }
        public int AuthoredPriority { get; set; }
    }

    public sealed class CharacterDamageZonesDocument
    {
        public const string CurrentFormat = "mx.characterDamageZones.v1";
        public const string CurrentSchemaVersion = "1.0";

        public string Format { get; set; } = CurrentFormat;
        public string SchemaVersion { get; set; } = CurrentSchemaVersion;
        public List<CharacterDamageZoneAuthoring> Zones { get; set; } = new List<CharacterDamageZoneAuthoring>();
    }

    public enum CharacterEngagementAnchorsDocumentState
    {
        Missing = 0,
        Current = 1,
        UnsupportedSchema = 2,
        Invalid = 3
    }

    public enum CharacterEngagementAnchorReferenceFrame
    {
        Unknown = 0,
        ModelRoot = 1,
        World = 2,
        SkeletonRoot = 3,
        Bone = 4,
        Locator = 5,
        BodyPart = 6,
        Socket = 7,
        Hurtbox = 8
    }

    public sealed class CharacterAuthoringIntVector3
    {
        public CharacterAuthoringIntVector3()
        {
        }

        public CharacterAuthoringIntVector3(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
    }

    /// <summary>Author-owned long-lived approach reference in ModelRoot-local deterministic units.</summary>
    public sealed class CharacterEngagementAnchorAuthoring
    {
        public string AnchorId { get; set; } = string.Empty;
        public string ZoneId { get; set; } = string.Empty;
        public CharacterEngagementAnchorReferenceFrame ReferenceFrame { get; set; } =
            CharacterEngagementAnchorReferenceFrame.ModelRoot;
        public CharacterAuthoringIntVector3 LocalPositionMillimeters { get; set; } =
            new CharacterAuthoringIntVector3();
        public int YawMilliDegrees { get; set; }
        public int Capacity { get; set; } = 1;
        public int AuthoredPriority { get; set; }
        public bool Enabled { get; set; } = true;
        public string PhaseId { get; set; } = string.Empty;
    }

    public sealed class CharacterEngagementAnchorsDocument
    {
        public const string CurrentFormat = "mx.characterEngagementAnchors.v1";
        public const string CurrentSchemaVersion = "1.0";

        public string Format { get; set; } = CurrentFormat;
        public string SchemaVersion { get; set; } = CurrentSchemaVersion;
        public int Revision { get; set; } = 1;
        public List<CharacterEngagementAnchorAuthoring> Anchors { get; set; } =
            new List<CharacterEngagementAnchorAuthoring>();
    }

    public sealed class CharacterSocketProfile
    {
        public string SocketId { get; set; } = string.Empty;
        public string AbstractBoneId { get; set; } = string.Empty;
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

    /// <summary>
    /// Character-owned semantic weapon slot. The pose is independent of the weapon selected by a combination.
    /// AllocByDesign: low-frequency authoring DTO.
    /// </summary>
    public sealed class CharacterWeaponSlotProfile
    {
        public string SlotId { get; set; } = string.Empty;
        public string AttachSocketId { get; set; } = string.Empty;
        public CharacterAuthoringLocalPose SlotPose { get; set; } = new CharacterAuthoringLocalPose();
    }

    public sealed class WeaponAttachmentProfile
    {
        public string AttachmentId { get; set; } = string.Empty;
        public string SingleWeaponConfigurationId { get; set; } = string.Empty;
        public string SlotId { get; set; } = string.Empty;
        public string AttachSocketId { get; set; } = string.Empty;
        public CharacterAuthoringLocalPose LocalGripPose { get; set; } = new CharacterAuthoringLocalPose();
        [JsonIgnore]
        // 1.0 migration-only data. Schema 3.0 owns this selection in the global single-weapon library.
        public AuthoringResourceSelectionRef WeaponRef { get; set; } = new AuthoringResourceSelectionRef();
        [JsonIgnore]
        public string PreviewResourceKey { get; set; } = string.Empty;
        [JsonIgnore]
        public AuthoringResourceSelectionRef PreviewResourceSelection { get; set; } = new AuthoringResourceSelectionRef();
        public string TraceId { get; set; } = string.Empty;
        public string TraceStartSocketId { get; set; } = string.Empty;
        public string TraceEndSocketId { get; set; } = string.Empty;
        public float TraceRadius { get; set; }
        public WeaponTraceSampleRule TraceSampleRule { get; set; } = WeaponTraceSampleRule.CapsuleSweep;

        [JsonIgnore]
        // Source-code alias for generated/runtime contracts. Schema 3.0 authoring documents use slotId.
        public string EquipSlot
        {
            get { return SlotId; }
            set { SlotId = value ?? string.Empty; }
        }

        [JsonPropertyName("equipSlot")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string LegacyEquipSlot
        {
            get { return null; }
            set
            {
                if (string.IsNullOrWhiteSpace(SlotId))
                    SlotId = value ?? string.Empty;
            }
        }

        [JsonPropertyName("weaponId")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string LegacyWeaponId
        {
            get { return null; }
            set
            {
                if (string.IsNullOrWhiteSpace(SingleWeaponConfigurationId))
                    SingleWeaponConfigurationId = value ?? string.Empty;
            }
        }

        [JsonPropertyName("weaponRef")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public AuthoringResourceSelectionRef LegacyWeaponRef
        {
            get { return null; }
            set { WeaponRef = value ?? new AuthoringResourceSelectionRef(); }
        }
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
        public float ProximalExtension { get; set; }
        public float ProximalRadius { get; set; }
        public WeaponTraceSampleRule SampleRule { get; set; } = WeaponTraceSampleRule.CapsuleSweep;
        public int FixedSampleCount { get; set; } = 4;
        public List<string> ActionKeys { get; set; } = new List<string>();
    }

    public sealed class CharacterBodyPartContactPointProfile
    {
        public string StableId { get; set; } = string.Empty;
        public int ContactPointId { get; set; }
        public int BodyPartId { get; set; }
        public string PartId { get; set; } = string.Empty;
        public CharacterAuthoringVector3 LocalOffsetMeters { get; set; } = new CharacterAuthoringVector3();
        public float RadiusMeters { get; set; }
        public string LayerMaskProfileId { get; set; } = string.Empty;
        public string Usage { get; set; } = "PresentationAudio";
        public string Authority { get; set; } = "PresentationOnly";
        public CharacterBodyPartContactActivationMode ActivationMode { get; set; } =
            CharacterBodyPartContactActivationMode.Always;
        public CharacterBodyPartContactMotionMode MotionMode { get; set; } =
            CharacterBodyPartContactMotionMode.Static;
        public string MotionSocketId { get; set; } = string.Empty;
        public int SourceOrder { get; set; }
    }

    public sealed partial class CharacterAuthoringGeometry
    {
        public string SchemaVersion { get; set; } = "1.0";
        public string WeaponSlotsSchemaVersion { get; set; } = "1.0";
        public CharacterBodyGeometryProfile BodyProfile { get; set; } = new CharacterBodyGeometryProfile();
        public List<CharacterBodyPartAuthoring> BodyParts { get; set; } = new List<CharacterBodyPartAuthoring>();
        public List<CharacterBodyColliderProfile> Colliders { get; set; } = new List<CharacterBodyColliderProfile>();
        public CharacterDamageZonesDocument DamageZones { get; set; }
        public CharacterDamageZonesDocumentState DamageZonesDocumentState { get; set; } =
            CharacterDamageZonesDocumentState.Missing;
        public string DamageZonesRawPayload { get; set; } = string.Empty;
        public bool DamageZonesWriteEligible =>
            DamageZonesDocumentState == CharacterDamageZonesDocumentState.Current;
        public CharacterEngagementAnchorsDocument EngagementAnchors { get; set; }
        public CharacterEngagementAnchorsDocumentState EngagementAnchorsDocumentState { get; set; } =
            CharacterEngagementAnchorsDocumentState.Missing;
        public string EngagementAnchorsRawPayload { get; set; } = string.Empty;
        public bool EngagementAnchorsWriteEligible =>
            EngagementAnchorsDocumentState == CharacterEngagementAnchorsDocumentState.Current;
        public List<CharacterSocketProfile> Sockets { get; set; } = new List<CharacterSocketProfile>();
        public List<CharacterWeaponSlotProfile> WeaponSlots { get; set; } = new List<CharacterWeaponSlotProfile>();
        [JsonIgnore]
        public string WeaponAttachmentsSchemaVersion { get; set; } = "3.0";
        [JsonIgnore]
        public List<WeaponAttachmentProfile> WeaponAttachments { get; set; } = new List<WeaponAttachmentProfile>();
        [JsonIgnore]
        public List<WeaponTraceProfile> Traces { get; set; } = new List<WeaponTraceProfile>();
        public List<CharacterBodyPartContactPointProfile> BodyPartContactPoints { get; set; } =
            new List<CharacterBodyPartContactPointProfile>();
    }
}

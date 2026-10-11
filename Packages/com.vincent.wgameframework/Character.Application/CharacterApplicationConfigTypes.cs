using System;
using System.Collections.Generic;
using MxFramework.Config;

namespace MxFramework.CharacterApplication
{
    public enum CharacterControllerKind
    {
        None = 0,
        HumanInput = 1,
        RuntimeAiPlanner = 2,
        Scripted = 3,
        Disabled = 4
    }

    public enum CharacterBodyKind
    {
        Custom = 0,
        Humanoid = 1,
        Quadruped = 2,
        Winged = 3,
        Serpentine = 4,
        Slime = 5,
        SimpleShape = 6
    }

    public enum CharacterBodyPartKind
    {
        Custom = 0,
        Root = 1,
        Head = 2,
        Torso = 3,
        Arm = 4,
        Hand = 5,
        Leg = 6,
        Foot = 7,
        Wing = 8,
        Tail = 9,
        Horn = 10,
        Core = 11,
        WeaponMount = 12
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

    public enum CharacterAttributeGroup
    {
        Custom = 0,
        Vital = 1,
        Resource = 2,
        Combat = 3,
        Mobility = 4,
        Pressure = 5
    }

    public enum EquipmentSlotKind
    {
        Custom = 0,
        MainHand = 1,
        OffHand = 2,
        TwoHand = 3,
        Head = 4,
        Body = 5,
        Back = 6,
        Accessory = 7,
        NaturalWeapon = 8
    }



    public enum CharacterResourceUsageKind
    {
        Custom = 0,
        Model = 1,
        AnimationProfile = 2,
        WeaponModel = 3,
        Vfx = 4,
        Sfx = 5,
        Ui = 6,
        Debug = 7
    }

    public readonly struct CharacterAttributeEntry
    {
        public CharacterAttributeEntry(
            CharacterAttributeId attributeId,
            string stableId,
            CharacterAttributeGroup group,
            float baseValue,
            float initialValue,
            float minValue,
            float maxValue)
        {
            AttributeId = attributeId;
            StableId = stableId ?? string.Empty;
            Group = group;
            BaseValue = baseValue;
            InitialValue = initialValue;
            MinValue = minValue;
            MaxValue = maxValue;
        }

        public CharacterAttributeId AttributeId { get; }
        public string StableId { get; }
        public CharacterAttributeGroup Group { get; }
        public float BaseValue { get; }
        public float InitialValue { get; }
        public float MinValue { get; }
        public float MaxValue { get; }
    }

    public readonly struct CharacterSocketEntry
    {
        public CharacterSocketEntry(int socketId, int parentPartId, string locatorId)
        {
            SocketId = socketId;
            ParentPartId = parentPartId;
            LocatorId = locatorId ?? string.Empty;
        }

        public int SocketId { get; }
        public int ParentPartId { get; }
        public string LocatorId { get; }
    }

    public readonly struct CharacterHitZoneBindingEntry
    {
        public CharacterHitZoneBindingEntry(
            int hitZoneId,
            int partId,
            int priority,
            bool isWeakPoint,
            float damageMultiplierOverride = 0f,
            float impactPowerScaleOverride = 0f)
        {
            HitZoneId = hitZoneId;
            PartId = partId;
            Priority = priority;
            IsWeakPoint = isWeakPoint;
            DamageMultiplierOverride = damageMultiplierOverride;
            ImpactPowerScaleOverride = impactPowerScaleOverride;
        }

        public int HitZoneId { get; }
        public int PartId { get; }
        public int Priority { get; }
        public bool IsWeakPoint { get; }
        public float DamageMultiplierOverride { get; }
        public float ImpactPowerScaleOverride { get; }
    }

    public readonly struct EquipmentSlotEntry
    {
        public EquipmentSlotEntry(
            int slotId,
            EquipmentSlotKind kind,
            string displayName,
            string[] allowedWeaponCategories,
            string[] requiredTags = null)
        {
            SlotId = slotId;
            Kind = kind;
            DisplayName = displayName ?? string.Empty;
            AllowedWeaponCategories = allowedWeaponCategories ?? Array.Empty<string>();
            RequiredTags = requiredTags ?? Array.Empty<string>();
        }

        public int SlotId { get; }
        public EquipmentSlotKind Kind { get; }
        public string DisplayName { get; }
        public string[] AllowedWeaponCategories { get; }
        public string[] RequiredTags { get; }
    }

    public readonly struct EquipmentExclusiveGroupEntry
    {
        public EquipmentExclusiveGroupEntry(string groupId, int[] slotIds, int maxFilledCount)
        {
            GroupId = groupId ?? string.Empty;
            SlotIds = slotIds ?? Array.Empty<int>();
            MaxFilledCount = maxFilledCount;
        }

        public string GroupId { get; }
        public int[] SlotIds { get; }
        public int MaxFilledCount { get; }
    }

    public readonly struct EquipmentLoadoutSlotEntry
    {
        /// <summary>NoAlloc: creates one immutable equipment slot entry.</summary>
        public EquipmentLoadoutSlotEntry(int slotId, WeaponConfigId weaponId, string weaponInstanceStableId = "")
            : this(slotId, weaponId, weaponInstanceStableId, string.Empty)
        {
        }

        /// <summary>NoAlloc: creates one immutable schema 3.0 equipment slot entry with an exact authoring attachment identity.</summary>
        public EquipmentLoadoutSlotEntry(int slotId, WeaponConfigId weaponId, string weaponInstanceStableId, string attachmentId)
        {
            SlotId = slotId;
            WeaponId = weaponId;
            WeaponInstanceStableId = weaponInstanceStableId ?? string.Empty;
            AttachmentId = attachmentId ?? string.Empty;
        }

        public int SlotId { get; }
        public WeaponConfigId WeaponId { get; }
        public string WeaponInstanceStableId { get; }
        public string AttachmentId { get; }
    }

    public readonly struct EquipmentMatchRule
    {
        public EquipmentMatchRule(
            int[] requiredFilledSlots,
            int[] requiredEmptySlots,
            string[] requiredWeaponCategoriesBySlot,
            string[] requiredWeaponTagsBySlot,
            string[] forbiddenWeaponTagsBySlot)
        {
            RequiredFilledSlots = requiredFilledSlots ?? Array.Empty<int>();
            RequiredEmptySlots = requiredEmptySlots ?? Array.Empty<int>();
            RequiredWeaponCategoriesBySlot = requiredWeaponCategoriesBySlot ?? Array.Empty<string>();
            RequiredWeaponTagsBySlot = requiredWeaponTagsBySlot ?? Array.Empty<string>();
            ForbiddenWeaponTagsBySlot = forbiddenWeaponTagsBySlot ?? Array.Empty<string>();
        }

        public int[] RequiredFilledSlots { get; }
        public int[] RequiredEmptySlots { get; }
        public string[] RequiredWeaponCategoriesBySlot { get; }
        public string[] RequiredWeaponTagsBySlot { get; }
        public string[] ForbiddenWeaponTagsBySlot { get; }
    }

    public readonly struct CharacterModifierGrantEntry
    {
        public CharacterModifierGrantEntry(string modifierStableId, float value, string source)
        {
            ModifierStableId = modifierStableId ?? string.Empty;
            Value = value;
            Source = source ?? string.Empty;
        }

        public string ModifierStableId { get; }
        public float Value { get; }
        public string Source { get; }
    }

    public readonly struct CharacterTraceBindingEntry
    {
        public CharacterTraceBindingEntry(
            string traceProfileId,
            int slotId,
            int socketId,
            float length,
            float radius,
            float proximalExtension = 0f,
            float proximalRadius = 0f)
        {
            TraceProfileId = traceProfileId ?? string.Empty;
            SlotId = slotId;
            SocketId = socketId;
            Length = length;
            Radius = radius;
            ProximalExtension = proximalExtension < 0f ? 0f : proximalExtension;
            ProximalRadius = proximalRadius < 0f ? 0f : proximalRadius;
        }

        public string TraceProfileId { get; }
        public int SlotId { get; }
        public int SocketId { get; }
        public float Length { get; }
        public float Radius { get; }
        public float ProximalExtension { get; }
        public float ProximalRadius { get; }
    }

    public readonly struct CharacterResourceKeyEntry
    {
        public CharacterResourceKeyEntry(
            string id,
            string typeId,
            CharacterResourceUsageKind usageKind,
            string variant = "",
            string packageId = "",
            string preloadGroupId = "")
        {
            Id = id ?? string.Empty;
            TypeId = typeId ?? string.Empty;
            UsageKind = usageKind;
            Variant = variant ?? string.Empty;
            PackageId = packageId ?? string.Empty;
            PreloadGroupId = preloadGroupId ?? string.Empty;
        }

        public string Id { get; }
        public string TypeId { get; }
        public CharacterResourceUsageKind UsageKind { get; }
        public string Variant { get; }
        public string PackageId { get; }
        public string PreloadGroupId { get; }
    }

    public readonly struct AbilitySlotBinding
    {
        public AbilitySlotBinding(int slotId, CharacterAbilityId abilityId, string inputIntentId)
        {
            SlotId = slotId;
            AbilityId = abilityId;
            InputIntentId = inputIntentId ?? string.Empty;
        }

        public int SlotId { get; }
        public CharacterAbilityId AbilityId { get; }
        public string InputIntentId { get; }
    }

    public readonly struct CombatActionEntry
    {
        public CombatActionEntry(
            string actionKey,
            CharacterCombatActionId combatActionId,
            string traceProfileIdOverride,
            string animationActionKey)
        {
            ActionKey = actionKey ?? string.Empty;
            CombatActionId = combatActionId;
            TraceProfileIdOverride = traceProfileIdOverride ?? string.Empty;
            AnimationActionKey = animationActionKey ?? string.Empty;
        }

        public string ActionKey { get; }
        public CharacterCombatActionId CombatActionId { get; }
        public string TraceProfileIdOverride { get; }
        public string AnimationActionKey { get; }
    }

    public readonly struct CharacterPoseEntry
    {
        public CharacterPoseEntry(string anchorId, float x, float y, float z, float yawDegrees)
        {
            AnchorId = anchorId ?? string.Empty;
            X = x;
            Y = y;
            Z = z;
            YawDegrees = yawDegrees;
        }

        public string AnchorId { get; }
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public float YawDegrees { get; }
    }

    public readonly struct CharacterSpawnRequest
    {
        public CharacterSpawnRequest(
            SpawnProfileId spawnProfileId,
            CharacterConfigId? characterOverride = null,
            EquipmentLoadoutId? loadoutOverride = null,
            string teamOverride = "",
            CharacterControllerKind? controllerOverride = null,
            CharacterPoseEntry? poseOverride = null,
            string debugNameOverride = "")
        {
            SpawnProfileId = spawnProfileId;
            CharacterOverride = characterOverride;
            LoadoutOverride = loadoutOverride;
            TeamOverride = teamOverride ?? string.Empty;
            ControllerOverride = controllerOverride;
            PoseOverride = poseOverride;
            DebugNameOverride = debugNameOverride ?? string.Empty;
        }

        public SpawnProfileId SpawnProfileId { get; }
        public CharacterConfigId? CharacterOverride { get; }
        public EquipmentLoadoutId? LoadoutOverride { get; }
        public string TeamOverride { get; }
        public CharacterControllerKind? ControllerOverride { get; }
        public CharacterPoseEntry? PoseOverride { get; }
        public string DebugNameOverride { get; }
    }

    internal static class CharacterConfigReferenceCollector
    {
        public static void AddRequired<TTarget>(
            ICollection<ConfigReference> references,
            Type ownerType,
            int ownerId,
            int targetId,
            string fieldName) where TTarget : IConfigData
        {
            if (references == null)
                return;

            references.Add(new ConfigReference(ownerType, ownerId, typeof(TTarget), targetId, fieldName));
        }

        public static void AddOptional<TTarget>(
            ICollection<ConfigReference> references,
            Type ownerType,
            int ownerId,
            int targetId,
            string fieldName) where TTarget : IConfigData
        {
            if (references == null || targetId <= 0)
                return;

            references.Add(new ConfigReference(ownerType, ownerId, typeof(TTarget), targetId, fieldName));
        }
    }

    public sealed partial class CharacterConfig : IConfigData, IConfigReferenceProvider
    {
        public const string TableName = "CharacterConfig";

        public CharacterConfig(
            CharacterConfigId characterId,
            string stableId,
            LocalizedTextKey nameText,
            LocalizedTextKey descriptionText,
            CharacterAttributeProfileId attributeProfileId,
            CharacterBodyProfileId bodyProfileId,
            EquipmentSchemaId equipmentSchemaId,
            EquipmentLoadoutId defaultLoadoutId,
            AbilityLoadoutId baseAbilityLoadoutId,
            CharacterPresentationProfileId presentationProfileId,
            CharacterControllerKind defaultControllerKind,
            string defaultControllerProfileId,
            string[] defaultSpawnTags)
        {
            CharacterId = characterId;
            StableId = stableId ?? string.Empty;
            NameText = nameText;
            DescriptionText = descriptionText;
            AttributeProfileId = attributeProfileId;
            BodyProfileId = bodyProfileId;
            EquipmentSchemaId = equipmentSchemaId;
            DefaultLoadoutId = defaultLoadoutId;
            BaseAbilityLoadoutId = baseAbilityLoadoutId;
            PresentationProfileId = presentationProfileId;
            DefaultControllerKind = defaultControllerKind;
            DefaultControllerProfileId = defaultControllerProfileId ?? string.Empty;
            DefaultSpawnTags = defaultSpawnTags ?? Array.Empty<string>();
        }

        public int Id => CharacterId.Value;
        public CharacterConfigId CharacterId { get; }
        public string StableId { get; }
        public LocalizedTextKey NameText { get; }
        public LocalizedTextKey DescriptionText { get; }
        public CharacterAttributeProfileId AttributeProfileId { get; }
        public CharacterBodyProfileId BodyProfileId { get; }
        public EquipmentSchemaId EquipmentSchemaId { get; }
        public EquipmentLoadoutId DefaultLoadoutId { get; }
        public AbilityLoadoutId BaseAbilityLoadoutId { get; }
        public CharacterPresentationProfileId PresentationProfileId { get; }
        public CharacterControllerKind DefaultControllerKind { get; }
        public string DefaultControllerProfileId { get; }
        public string[] DefaultSpawnTags { get; }

        public void CollectReferences(ICollection<ConfigReference> references)
        {
            CharacterConfigReferenceCollector.AddRequired<CharacterAttributeProfileConfig>(references, typeof(CharacterConfig), Id, AttributeProfileId.Value, nameof(AttributeProfileId));
            CharacterConfigReferenceCollector.AddRequired<CharacterBodyProfileConfig>(references, typeof(CharacterConfig), Id, BodyProfileId.Value, nameof(BodyProfileId));
            CharacterConfigReferenceCollector.AddRequired<EquipmentSchemaConfig>(references, typeof(CharacterConfig), Id, EquipmentSchemaId.Value, nameof(EquipmentSchemaId));
            CharacterConfigReferenceCollector.AddRequired<EquipmentLoadoutConfig>(references, typeof(CharacterConfig), Id, DefaultLoadoutId.Value, nameof(DefaultLoadoutId));
            CharacterConfigReferenceCollector.AddOptional<AbilityLoadoutConfig>(references, typeof(CharacterConfig), Id, BaseAbilityLoadoutId.Value, nameof(BaseAbilityLoadoutId));
            CharacterConfigReferenceCollector.AddRequired<CharacterPresentationProfileConfig>(references, typeof(CharacterConfig), Id, PresentationProfileId.Value, nameof(PresentationProfileId));
        }
    }

    public sealed partial class CharacterAttributeProfileConfig : IConfigData
    {
        public const string TableName = "CharacterAttributeProfileConfig";

        public CharacterAttributeProfileConfig(
            CharacterAttributeProfileId attributeProfileId,
            string stableId,
            CharacterAttributeEntry[] attributes)
            : this(attributeProfileId, stableId, attributes, 100, 2, 360, 270)
        {
        }

        public CharacterAttributeProfileConfig(
            CharacterAttributeProfileId attributeProfileId,
            string stableId,
            CharacterAttributeEntry[] attributes,
            int baseCohesion = 100,
            int scatterRecoveryRate = 2,
            int scatterRecoveryDelayFrames = 360,
            int cohesionBreakRecoveryDelayFrames = 270)
        {
            AttributeProfileId = attributeProfileId;
            StableId = stableId ?? string.Empty;
            Attributes = attributes ?? Array.Empty<CharacterAttributeEntry>();
            if (baseCohesion <= 0 || scatterRecoveryRate < 0 || scatterRecoveryDelayFrames < 0 || cohesionBreakRecoveryDelayFrames < 0)
                throw new ArgumentOutOfRangeException(nameof(baseCohesion), "Invalid cohesion or recovery settings.");
            BaseCohesion = baseCohesion;
            ScatterRecoveryRate = scatterRecoveryRate;
            ScatterRecoveryDelayFrames = scatterRecoveryDelayFrames;
            CohesionBreakRecoveryDelayFrames = cohesionBreakRecoveryDelayFrames;
        }

        public int Id => AttributeProfileId.Value;
        public CharacterAttributeProfileId AttributeProfileId { get; }
        public string StableId { get; }
        public CharacterAttributeEntry[] Attributes { get; }
        public int BaseCohesion { get; }
        public int ScatterRecoveryRate { get; }
        public int ScatterRecoveryDelayFrames { get; }
        public int CohesionBreakRecoveryDelayFrames { get; }
    }

    public sealed partial class CharacterBodyProfileConfig : IConfigData
    {
        public const string TableName = "CharacterBodyProfileConfig";

        public CharacterBodyProfileConfig(
            CharacterBodyProfileId bodyProfileId,
            string stableId,
            CharacterBodyKind bodyKind,
            string partSetId,
            string defaultMotionProfileId,
            string defaultPhysicsProfileId,
            CharacterSocketEntry[] sockets,
            CharacterHitZoneBindingEntry[] hitZoneBindings)
        {
            BodyProfileId = bodyProfileId;
            StableId = stableId ?? string.Empty;
            BodyKind = bodyKind;
            PartSetId = partSetId ?? string.Empty;
            DefaultMotionProfileId = defaultMotionProfileId ?? string.Empty;
            DefaultPhysicsProfileId = defaultPhysicsProfileId ?? string.Empty;
            Sockets = sockets ?? Array.Empty<CharacterSocketEntry>();
            HitZoneBindings = hitZoneBindings ?? Array.Empty<CharacterHitZoneBindingEntry>();
        }

        public int Id => BodyProfileId.Value;
        public CharacterBodyProfileId BodyProfileId { get; }
        public string StableId { get; }
        public CharacterBodyKind BodyKind { get; }
        public string PartSetId { get; }
        public string DefaultMotionProfileId { get; }
        public string DefaultPhysicsProfileId { get; }
        public CharacterSocketEntry[] Sockets { get; }
        public CharacterHitZoneBindingEntry[] HitZoneBindings { get; }
    }

    public sealed partial class CharacterBodyPartConfig : IConfigData
    {
        public const string TableName = "CharacterBodyPartConfig";

        public CharacterBodyPartConfig(
            CharacterBodyPartConfigId bodyPartConfigId,
            string stableId,
            string partSetId,
            int partId,
            int parentPartId,
            CharacterBodyPartKind partKind,
            string locatorId,
            int hitZoneId,
            int reactionGroupId,
            float damageMultiplier,
            float impulseScale,
            float staggerScale,
            float impactPowerScale,
            bool isCritical)
        {
            BodyPartConfigId = bodyPartConfigId;
            StableId = stableId ?? string.Empty;
            PartSetId = partSetId ?? string.Empty;
            PartId = partId;
            ParentPartId = parentPartId;
            PartKind = partKind;
            LocatorId = locatorId ?? string.Empty;
            HitZoneId = hitZoneId;
            ReactionGroupId = reactionGroupId;
            DamageMultiplier = damageMultiplier;
            ImpulseScale = impulseScale;
            StaggerScale = staggerScale;
            ImpactPowerScale = impactPowerScale;
            IsCritical = isCritical;
        }

        public int Id => BodyPartConfigId.Value;
        public CharacterBodyPartConfigId BodyPartConfigId { get; }
        public string StableId { get; }
        public string PartSetId { get; }
        public int PartId { get; }
        public int ParentPartId { get; }
        public CharacterBodyPartKind PartKind { get; }
        public string LocatorId { get; }
        public int HitZoneId { get; }
        public int ReactionGroupId { get; }
        public float DamageMultiplier { get; }
        public float ImpulseScale { get; }
        public float StaggerScale { get; }
        public float ImpactPowerScale { get; }
        public bool IsCritical { get; }
    }

    public sealed partial class CharacterBodyPartContactPointConfig : IConfigData
    {
        public const string TableName = "CharacterBodyPartContactPointConfig";

        public CharacterBodyPartContactPointConfig(
            CharacterBodyPartContactPointConfigId contactPointConfigId,
            string stableId,
            string partSetId,
            int partId,
            int contactPointId,
            int bodyPartId,
            int localOffsetXMillimeters,
            int localOffsetYMillimeters,
            int localOffsetZMillimeters,
            int radiusMillimeters,
            string layerMaskProfileId,
            CharacterBodyPartContactActivationMode activationMode,
            CharacterBodyPartContactMotionMode motionMode,
            int motionSocketId,
            string usage,
            string authority,
            int sourceOrder)
        {
            ContactPointConfigId = contactPointConfigId;
            StableId = stableId ?? string.Empty;
            PartSetId = partSetId ?? string.Empty;
            PartId = partId;
            ContactPointId = contactPointId < 0 ? 0 : contactPointId;
            BodyPartId = bodyPartId < 0 ? 0 : bodyPartId;
            LocalOffsetXMillimeters = localOffsetXMillimeters;
            LocalOffsetYMillimeters = localOffsetYMillimeters;
            LocalOffsetZMillimeters = localOffsetZMillimeters;
            RadiusMillimeters = radiusMillimeters < 0 ? 0 : radiusMillimeters;
            LayerMaskProfileId = layerMaskProfileId ?? string.Empty;
            ActivationMode = activationMode;
            MotionMode = motionMode;
            MotionSocketId = motionSocketId;
            Usage = string.IsNullOrWhiteSpace(usage) ? "PresentationAudio" : usage;
            Authority = string.IsNullOrWhiteSpace(authority) ? "PresentationOnly" : authority;
            SourceOrder = sourceOrder < 0 ? 0 : sourceOrder;
        }

        public int Id => ContactPointConfigId.Value;
        public CharacterBodyPartContactPointConfigId ContactPointConfigId { get; }
        public string StableId { get; }
        public string PartSetId { get; }
        public int PartId { get; }
        public int ContactPointId { get; }
        public int BodyPartId { get; }
        public int LocalOffsetXMillimeters { get; }
        public int LocalOffsetYMillimeters { get; }
        public int LocalOffsetZMillimeters { get; }
        public int RadiusMillimeters { get; }
        public string LayerMaskProfileId { get; }
        public CharacterBodyPartContactActivationMode ActivationMode { get; }
        public CharacterBodyPartContactMotionMode MotionMode { get; }
        public int MotionSocketId { get; }
        public string Usage { get; }
        public string Authority { get; }
        public int SourceOrder { get; }
    }

    public sealed partial class EquipmentSchemaConfig : IConfigData
    {
        public const string TableName = "EquipmentSchemaConfig";

        public EquipmentSchemaConfig(
            EquipmentSchemaId equipmentSchemaId,
            string stableId,
            EquipmentSlotEntry[] slots,
            EquipmentExclusiveGroupEntry[] exclusiveGroups,
            EquipmentStateId[] allowedStateIds)
        {
            EquipmentSchemaId = equipmentSchemaId;
            StableId = stableId ?? string.Empty;
            Slots = slots ?? Array.Empty<EquipmentSlotEntry>();
            ExclusiveGroups = exclusiveGroups ?? Array.Empty<EquipmentExclusiveGroupEntry>();
            AllowedStateIds = allowedStateIds ?? Array.Empty<EquipmentStateId>();
        }

        public int Id => EquipmentSchemaId.Value;
        public EquipmentSchemaId EquipmentSchemaId { get; }
        public string StableId { get; }
        public EquipmentSlotEntry[] Slots { get; }
        public EquipmentExclusiveGroupEntry[] ExclusiveGroups { get; }
        public EquipmentStateId[] AllowedStateIds { get; }
    }

    public sealed partial class EquipmentLoadoutConfig : IConfigData, IConfigReferenceProvider
    {
        public const string TableName = "EquipmentLoadoutConfig";

        public EquipmentLoadoutConfig(
            EquipmentLoadoutId loadoutId,
            string stableId,
            EquipmentSchemaId equipmentSchemaId,
            EquipmentLoadoutSlotEntry[] slots)
            : this(loadoutId, stableId, equipmentSchemaId, default, slots)
        {
        }

        /// <summary>
        /// Creates a structured equipment loadout with its exact state identity.
        /// LowFreqAlloc: retains the caller-provided slot array and performs no per-frame work.
        /// </summary>
        public EquipmentLoadoutConfig(
            EquipmentLoadoutId loadoutId,
            string stableId,
            EquipmentSchemaId equipmentSchemaId,
            EquipmentStateId equipmentStateId,
            EquipmentLoadoutSlotEntry[] slots)
        {
            LoadoutId = loadoutId;
            StableId = stableId ?? string.Empty;
            EquipmentSchemaId = equipmentSchemaId;
            EquipmentStateId = equipmentStateId;
            Slots = slots ?? Array.Empty<EquipmentLoadoutSlotEntry>();
        }

        public int Id => LoadoutId.Value;
        public EquipmentLoadoutId LoadoutId { get; }
        public string StableId { get; }
        public EquipmentSchemaId EquipmentSchemaId { get; }
        public EquipmentStateId EquipmentStateId { get; }
        public EquipmentLoadoutSlotEntry[] Slots { get; }

        public void CollectReferences(ICollection<ConfigReference> references)
        {
            CharacterConfigReferenceCollector.AddRequired<EquipmentSchemaConfig>(references, typeof(EquipmentLoadoutConfig), Id, EquipmentSchemaId.Value, nameof(EquipmentSchemaId));
            CharacterConfigReferenceCollector.AddOptional<EquipmentStateConfig>(references, typeof(EquipmentLoadoutConfig), Id, EquipmentStateId.Value, nameof(EquipmentStateId));
            for (int i = 0; i < Slots.Length; i++)
            {
                CharacterConfigReferenceCollector.AddOptional<WeaponConfig>(references, typeof(EquipmentLoadoutConfig), Id, Slots[i].WeaponId.Value, "Slots[" + i + "].WeaponId");
            }
        }
    }

    public sealed partial class EquipmentStateConfig : IConfigData, IConfigReferenceProvider
    {
        public const string TableName = "EquipmentStateConfig";

        public EquipmentStateConfig(
            EquipmentStateId stateId,
            string stableId,
            EquipmentSchemaId equipmentSchemaId,
            int priority,
            EquipmentMatchRule[] matchRules,
            AbilityLoadoutId grantedAbilityLoadoutId,
            CombatActionSetId combatActionSetId,
            string animationProfileId,
            string[] tags)
        {
            StateId = stateId;
            StableId = stableId ?? string.Empty;
            EquipmentSchemaId = equipmentSchemaId;
            Priority = priority;
            MatchRules = matchRules ?? Array.Empty<EquipmentMatchRule>();
            GrantedAbilityLoadoutId = grantedAbilityLoadoutId;
            CombatActionSetId = combatActionSetId;
            AnimationProfileId = animationProfileId ?? string.Empty;
            Tags = tags ?? Array.Empty<string>();
        }

        public int Id => StateId.Value;
        public EquipmentStateId StateId { get; }
        public string StableId { get; }
        public EquipmentSchemaId EquipmentSchemaId { get; }
        public int Priority { get; }
        public EquipmentMatchRule[] MatchRules { get; }
        public AbilityLoadoutId GrantedAbilityLoadoutId { get; }
        public CombatActionSetId CombatActionSetId { get; }
        public string AnimationProfileId { get; }
        public string[] Tags { get; }

        public void CollectReferences(ICollection<ConfigReference> references)
        {
            CharacterConfigReferenceCollector.AddRequired<EquipmentSchemaConfig>(references, typeof(EquipmentStateConfig), Id, EquipmentSchemaId.Value, nameof(EquipmentSchemaId));
            CharacterConfigReferenceCollector.AddOptional<AbilityLoadoutConfig>(references, typeof(EquipmentStateConfig), Id, GrantedAbilityLoadoutId.Value, nameof(GrantedAbilityLoadoutId));
            CharacterConfigReferenceCollector.AddRequired<CombatActionSetConfig>(references, typeof(EquipmentStateConfig), Id, CombatActionSetId.Value, nameof(CombatActionSetId));
        }
    }

    public enum WeaponBladeTrailStyle
    {
        ResourceDefault = 0,
        ThinBlade = 1,
        SoftRibbon = 2,
        DoubleBlade = 3,
        Energy = 4,
    }

    public sealed partial class WeaponConfig : IConfigData, IConfigReferenceProvider
    {
        public const string TableName = "WeaponConfig";

        public WeaponConfig(
            WeaponConfigId weaponId,
            string stableId,
            WeaponCategory category,
            string[] tags,
            int[] occupiesSlots,
            AbilityLoadoutId grantedAbilityLoadoutId,
            string weaponPresentationProfileId,
            string weaponCombatProfileId,
            string weaponResourceProfileId,
            CharacterResourceKeyEntry[] resourceKeys,
            CharacterTraceBindingEntry[] traceBindings,
            CharacterModifierGrantEntry[] baseModifiers,
            string bladeTrailResourceKey = "",
            int bladeTrailResidueMilliseconds = 100,
            string weaponTypeId = "")
            : this(weaponId, stableId, category, tags, occupiesSlots, grantedAbilityLoadoutId,
                weaponPresentationProfileId, weaponCombatProfileId, weaponResourceProfileId,
                resourceKeys, traceBindings, baseModifiers, bladeTrailResourceKey, bladeTrailResidueMilliseconds, 0, weaponTypeId)
        {
        }

        public WeaponConfig(
            WeaponConfigId weaponId, string stableId, WeaponCategory category, string[] tags,
            int[] occupiesSlots, AbilityLoadoutId grantedAbilityLoadoutId,
            string weaponPresentationProfileId, string weaponCombatProfileId, string weaponResourceProfileId,
            CharacterResourceKeyEntry[] resourceKeys, CharacterTraceBindingEntry[] traceBindings,
            CharacterModifierGrantEntry[] baseModifiers, string bladeTrailResourceKey,
            int bladeTrailResidueMilliseconds, int bladeTrailStyleId, string weaponTypeId = "")
        {
            WeaponId = weaponId;
            BladeTrailResourceKey = bladeTrailResourceKey ?? string.Empty;
            BladeTrailResidueMilliseconds = bladeTrailResidueMilliseconds;
            BladeTrailStyleId = bladeTrailStyleId;
            WeaponTypeId = weaponTypeId ?? string.Empty;
            StableId = stableId ?? string.Empty;
            Category = category;
            Tags = tags ?? Array.Empty<string>();
            OccupiesSlots = occupiesSlots ?? Array.Empty<int>();
            GrantedAbilityLoadoutId = grantedAbilityLoadoutId;
            WeaponPresentationProfileId = weaponPresentationProfileId ?? string.Empty;
            WeaponCombatProfileId = weaponCombatProfileId ?? string.Empty;
            WeaponResourceProfileId = weaponResourceProfileId ?? string.Empty;
            ResourceKeys = resourceKeys ?? Array.Empty<CharacterResourceKeyEntry>();
            TraceBindings = traceBindings ?? Array.Empty<CharacterTraceBindingEntry>();
            BaseModifiers = baseModifiers ?? Array.Empty<CharacterModifierGrantEntry>();
        }

        public int Id => WeaponId.Value;
        public WeaponConfigId WeaponId { get; }
        public string StableId { get; }
        public WeaponCategory Category { get; }
        public string WeaponTypeId { get; }
        public string[] Tags { get; }
        public int[] OccupiesSlots { get; }
        public AbilityLoadoutId GrantedAbilityLoadoutId { get; }
        public string BladeTrailResourceKey { get; }
        public int BladeTrailResidueMilliseconds { get; }
        public int BladeTrailStyleId { get; }
        public string WeaponPresentationProfileId { get; }
        public string WeaponCombatProfileId { get; }
        public string WeaponResourceProfileId { get; }
        public CharacterResourceKeyEntry[] ResourceKeys { get; }
        public CharacterTraceBindingEntry[] TraceBindings { get; }
        public CharacterModifierGrantEntry[] BaseModifiers { get; }

        public void CollectReferences(ICollection<ConfigReference> references)
        {
            CharacterConfigReferenceCollector.AddOptional<AbilityLoadoutConfig>(references, typeof(WeaponConfig), Id, GrantedAbilityLoadoutId.Value, nameof(GrantedAbilityLoadoutId));
        }
    }

    public sealed partial class AbilityLoadoutConfig : IConfigData
    {
        public const string TableName = "AbilityLoadoutConfig";

        public AbilityLoadoutConfig(
            AbilityLoadoutId loadoutId,
            string stableId,
            CharacterAbilityId[] abilityIds,
            CharacterAbilityId[] removeAbilityIds,
            AbilitySlotBinding[] slotBindings)
        {
            LoadoutId = loadoutId;
            StableId = stableId ?? string.Empty;
            AbilityIds = abilityIds ?? Array.Empty<CharacterAbilityId>();
            RemoveAbilityIds = removeAbilityIds ?? Array.Empty<CharacterAbilityId>();
            SlotBindings = slotBindings ?? Array.Empty<AbilitySlotBinding>();
        }

        public int Id => LoadoutId.Value;
        public AbilityLoadoutId LoadoutId { get; }
        public string StableId { get; }
        public CharacterAbilityId[] AbilityIds { get; }
        public CharacterAbilityId[] RemoveAbilityIds { get; }
        public AbilitySlotBinding[] SlotBindings { get; }
    }

    public sealed partial class CombatActionSetConfig : IConfigData
    {
        public const string TableName = "CombatActionSetConfig";

        public CombatActionSetConfig(CombatActionSetId actionSetId, string stableId, CombatActionEntry[] actions)
        {
            ActionSetId = actionSetId;
            StableId = stableId ?? string.Empty;
            Actions = actions ?? Array.Empty<CombatActionEntry>();
        }

        public int Id => ActionSetId.Value;
        public CombatActionSetId ActionSetId { get; }
        public string StableId { get; }
        public CombatActionEntry[] Actions { get; }
    }

    public readonly struct CharacterGameplayCameraVector3
    {
        public CharacterGameplayCameraVector3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public float X { get; }
        public float Y { get; }
        public float Z { get; }
    }

    public sealed class CharacterGameplayCameraNormalSettings
    {
        public CharacterGameplayCameraNormalSettings(
            float distance, float minDistance, float maxDistance, float fieldOfView,
            float pitch, float yawOffset, float targetPadding)
        {
            Distance = distance;
            MinDistance = minDistance;
            MaxDistance = maxDistance;
            FieldOfView = fieldOfView;
            Pitch = pitch;
            YawOffset = yawOffset;
            TargetPadding = targetPadding;
        }

        public float Distance { get; }
        public float MinDistance { get; }
        public float MaxDistance { get; }
        public float FieldOfView { get; }
        public float Pitch { get; }
        public float YawOffset { get; }
        public float TargetPadding { get; }
    }

    public sealed class CharacterGameplayCameraLockedSettings
    {
        public CharacterGameplayCameraLockedSettings(
            float minDistance, float maxDistance, float targetScreenY, float targetPadding,
            float framingScale = 1f)
        {
            MinDistance = minDistance;
            MaxDistance = maxDistance;
            TargetScreenY = targetScreenY;
            TargetPadding = targetPadding;
            FramingScale = framingScale;
        }

        public float MinDistance { get; }
        public float MaxDistance { get; }
        public float TargetScreenY { get; }
        public float TargetPadding { get; }
        public float FramingScale { get; }
    }

    public sealed class CharacterGameplayCameraObstructionSettings
    {
        public CharacterGameplayCameraObstructionSettings(
            float radius, float padding, float minDistance, float focusClearance)
        {
            Radius = radius;
            Padding = padding;
            MinDistance = minDistance;
            FocusClearance = focusClearance;
        }

        public float Radius { get; }
        public float Padding { get; }
        public float MinDistance { get; }
        public float FocusClearance { get; }
    }

    public sealed class CharacterGameplayCameraProfile
    {
        public CharacterGameplayCameraProfile(
            string profileId,
            string focusAbstractBoneId,
            CharacterGameplayCameraVector3 focusOffset,
            CharacterGameplayCameraVector3 framingBoundsCenter,
            CharacterGameplayCameraVector3 framingBoundsExtents,
            CharacterGameplayCameraNormalSettings normal,
            CharacterGameplayCameraLockedSettings locked,
            CharacterGameplayCameraObstructionSettings obstruction)
        {
            ProfileId = profileId ?? string.Empty;
            FocusAbstractBoneId = focusAbstractBoneId ?? string.Empty;
            FocusOffset = focusOffset;
            FramingBoundsCenter = framingBoundsCenter;
            FramingBoundsExtents = framingBoundsExtents;
            Normal = normal;
            Locked = locked;
            Obstruction = obstruction;
        }

        public string ProfileId { get; }
        public string FocusAbstractBoneId { get; }
        public CharacterGameplayCameraVector3 FocusOffset { get; }
        public CharacterGameplayCameraVector3 FramingBoundsCenter { get; }
        public CharacterGameplayCameraVector3 FramingBoundsExtents { get; }
        public CharacterGameplayCameraNormalSettings Normal { get; }
        public CharacterGameplayCameraLockedSettings Locked { get; }
        public CharacterGameplayCameraObstructionSettings Obstruction { get; }
    }

    public sealed partial class CharacterPresentationProfileConfig : IConfigData
    {
        public const string TableName = "CharacterPresentationProfileConfig";

        public CharacterPresentationProfileConfig(
            CharacterPresentationProfileId presentationProfileId,
            string stableId,
            string defaultAnimationProfileId,
            CharacterResourceKeyEntry[] resourceKeys,
            string[] presentationTags,
            CharacterGameplayCameraProfile gameplayCameraProfile = null,
            CharacterPresentationImages presentationImages = null)
        {
            PresentationProfileId = presentationProfileId;
            StableId = stableId ?? string.Empty;
            DefaultAnimationProfileId = defaultAnimationProfileId ?? string.Empty;
            ResourceKeys = resourceKeys ?? Array.Empty<CharacterResourceKeyEntry>();
            PresentationTags = presentationTags ?? Array.Empty<string>();
            GameplayCameraProfile = gameplayCameraProfile;
            PresentationImages = presentationImages;
        }

        public int Id => PresentationProfileId.Value;
        public CharacterPresentationProfileId PresentationProfileId { get; }
        public string StableId { get; }
        public string DefaultAnimationProfileId { get; }
        public CharacterResourceKeyEntry[] ResourceKeys { get; }
        public string[] PresentationTags { get; }
        public CharacterGameplayCameraProfile GameplayCameraProfile { get; }
        public CharacterPresentationImages PresentationImages { get; }
    }

    public sealed partial class SpawnProfileConfig : IConfigData, IConfigReferenceProvider
    {
        public const string TableName = "SpawnProfileConfig";

        public SpawnProfileConfig(
            SpawnProfileId spawnProfileId,
            string stableId,
            CharacterConfigId characterId,
            string teamId,
            CharacterControllerKind controllerKind,
            EquipmentLoadoutId equipmentLoadoutId,
            CharacterPoseEntry spawnPose,
            string runtimeAiPlannerProfileId,
            string debugName)
        {
            SpawnProfileId = spawnProfileId;
            StableId = stableId ?? string.Empty;
            CharacterId = characterId;
            TeamId = teamId ?? string.Empty;
            ControllerKind = controllerKind;
            EquipmentLoadoutId = equipmentLoadoutId;
            SpawnPose = spawnPose;
            RuntimeAiPlannerProfileId = runtimeAiPlannerProfileId ?? string.Empty;
            DebugName = debugName ?? string.Empty;
        }

        public int Id => SpawnProfileId.Value;
        public SpawnProfileId SpawnProfileId { get; }
        public string StableId { get; }
        public CharacterConfigId CharacterId { get; }
        public string TeamId { get; }
        public CharacterControllerKind ControllerKind { get; }
        public EquipmentLoadoutId EquipmentLoadoutId { get; }
        public CharacterPoseEntry SpawnPose { get; }
        public string RuntimeAiPlannerProfileId { get; }
        public string DebugName { get; }

        public void CollectReferences(ICollection<ConfigReference> references)
        {
            CharacterConfigReferenceCollector.AddRequired<CharacterConfig>(references, typeof(SpawnProfileConfig), Id, CharacterId.Value, nameof(CharacterId));
            CharacterConfigReferenceCollector.AddOptional<EquipmentLoadoutConfig>(references, typeof(SpawnProfileConfig), Id, EquipmentLoadoutId.Value, nameof(EquipmentLoadoutId));
        }
    }
}

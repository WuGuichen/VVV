using System;

namespace MxFramework.CharacterAction
{
    public enum CharacterActionCategory
    {
        None = 0,
        Idle = 1,
        Movement = 2,
        BasicAttack = 10,
        Skill = 11,
        Guard = 12,
        Dodge = 13,
        Jump = 14,
        Interaction = 15,
        Reaction = 20,
        PassiveOverlay = 30,
    }

    public enum CharacterMovementMode
    {
        Idle = 0,
        Walk = 1,
        Run = 2,
        Strafe = 3,
        TurnInPlace = 4,
        ApproachTarget = 5,
        Retreat = 6,
        CircleTarget = 7,
        RootMotionDriven = 8,
        Airborne = 9,
        ControlLocked = 10,
    }

    public enum CharacterActionRequirementKind
    {
        None = 0,
        Grounded = 1,
        Airborne = 2,
        EquipmentTagRequired = 3,
        EquipmentTagForbidden = 4,
        StatusRequired = 5,
        StatusForbidden = 6,
        ResourceAvailable = 7,
        CooldownReady = 8,
        TargetValid = 9,
        PhaseAllowed = 10,
    }

    public sealed class CharacterActionSetConfig
    {
        public CharacterActionSetConfig(
            int id,
            string stableId,
            string displayName,
            string characterStableId,
            string equipmentStateStableId,
            CharacterActionBinding[] commandBindings,
            CharacterAbilityActionBinding[] abilityBindings,
            CharacterReactionBinding[] reactionBindings,
            string movementProfileId,
            string reactionProfileId,
            string defaultActionId)
        {
            Id = id;
            StableId = stableId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            CharacterStableId = characterStableId ?? string.Empty;
            EquipmentStateStableId = equipmentStateStableId ?? string.Empty;
            CommandBindings = commandBindings ?? Array.Empty<CharacterActionBinding>();
            AbilityBindings = abilityBindings ?? Array.Empty<CharacterAbilityActionBinding>();
            ReactionBindings = reactionBindings ?? Array.Empty<CharacterReactionBinding>();
            MovementProfileId = movementProfileId ?? string.Empty;
            ReactionProfileId = reactionProfileId ?? string.Empty;
            DefaultActionId = defaultActionId ?? string.Empty;
        }

        public int Id { get; }
        public string StableId { get; }
        public string DisplayName { get; }
        public string CharacterStableId { get; }
        public string EquipmentStateStableId { get; }
        public CharacterActionBinding[] CommandBindings { get; }
        public CharacterAbilityActionBinding[] AbilityBindings { get; }
        public CharacterReactionBinding[] ReactionBindings { get; }
        public string MovementProfileId { get; }
        public string ReactionProfileId { get; }
        public string DefaultActionId { get; }
    }

    public sealed class CharacterActionConfig
    {
        public CharacterActionConfig(
            int id,
            string stableId,
            string displayName,
            CharacterActionCategory category,
            CharacterActionTimelineAuthority timelineAuthority,
            string[] tags,
            int priority,
            int? durationFrames,
            CharacterActionRequirement[] requirements,
            CharacterActionPhase[] phases,
            CharacterCancelRule[] cancelRules,
            CharacterInterruptRule[] interruptRules,
            MotionTrackConfig motionTrack = null,
            CombatTrackConfig combatTrack = null,
            GameplayTrackConfig gameplayTrack = null,
            AnimationTrackConfig animationTrack = null,
            PresentationTrackConfig presentationTrack = null,
            DebugTrackConfig debugTrack = null)
        {
            if (!Enum.IsDefined(typeof(CharacterActionCategory), category))
                throw new ArgumentOutOfRangeException(nameof(category), "Action category is not defined.");
            if (!Enum.IsDefined(typeof(CharacterActionTimelineAuthority), timelineAuthority))
                throw new ArgumentOutOfRangeException(nameof(timelineAuthority), "Timeline authority is not defined.");
            if (durationFrames.HasValue && durationFrames.Value < 0)
                throw new ArgumentOutOfRangeException(nameof(durationFrames), "Duration frames cannot be negative.");

            Id = id;
            StableId = stableId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Category = category;
            TimelineAuthority = timelineAuthority;
            Tags = tags ?? Array.Empty<string>();
            Priority = priority;
            DurationFrames = durationFrames;
            Requirements = requirements ?? Array.Empty<CharacterActionRequirement>();
            Phases = phases ?? Array.Empty<CharacterActionPhase>();
            CancelRules = cancelRules ?? Array.Empty<CharacterCancelRule>();
            InterruptRules = interruptRules ?? Array.Empty<CharacterInterruptRule>();
            MotionTrack = motionTrack ?? MotionTrackConfig.Empty;
            CombatTrack = combatTrack ?? CombatTrackConfig.Empty;
            GameplayTrack = gameplayTrack ?? GameplayTrackConfig.Empty;
            AnimationTrack = animationTrack ?? AnimationTrackConfig.Empty;
            PresentationTrack = presentationTrack ?? PresentationTrackConfig.Empty;
            DebugTrack = debugTrack ?? DebugTrackConfig.Empty;
        }

        public int Id { get; }
        public string StableId { get; }
        public string DisplayName { get; }
        public CharacterActionCategory Category { get; }
        public CharacterActionTimelineAuthority TimelineAuthority { get; }
        public string[] Tags { get; }
        public int Priority { get; }
        public int? DurationFrames { get; }
        public CharacterActionRequirement[] Requirements { get; }
        public CharacterActionPhase[] Phases { get; }
        public CharacterCancelRule[] CancelRules { get; }
        public CharacterInterruptRule[] InterruptRules { get; }
        public MotionTrackConfig MotionTrack { get; }
        public CombatTrackConfig CombatTrack { get; }
        public GameplayTrackConfig GameplayTrack { get; }
        public AnimationTrackConfig AnimationTrack { get; }
        public PresentationTrackConfig PresentationTrack { get; }
        public DebugTrackConfig DebugTrack { get; }
    }

    public sealed class CharacterMovementProfileConfig
    {
        public CharacterMovementProfileConfig(
            string stableId,
            CharacterMovementMode defaultMode,
            float walkSpeed,
            float runSpeed,
            float acceleration,
            float deceleration,
            float turnSpeed,
            float groundFriction,
            float airControl,
            float gravity,
            float jumpImpulse,
            float slopeLimitDegrees,
            string locomotionBlendId)
        {
            if (!Enum.IsDefined(typeof(CharacterMovementMode), defaultMode))
                throw new ArgumentOutOfRangeException(nameof(defaultMode), "Movement mode is not defined.");

            StableId = stableId ?? string.Empty;
            DefaultMode = defaultMode;
            WalkSpeed = walkSpeed;
            RunSpeed = runSpeed;
            Acceleration = acceleration;
            Deceleration = deceleration;
            TurnSpeed = turnSpeed;
            GroundFriction = groundFriction;
            AirControl = airControl;
            Gravity = gravity;
            JumpImpulse = jumpImpulse;
            SlopeLimitDegrees = slopeLimitDegrees;
            LocomotionBlendId = locomotionBlendId ?? string.Empty;
        }

        public string StableId { get; }
        public CharacterMovementMode DefaultMode { get; }
        public float WalkSpeed { get; }
        public float RunSpeed { get; }
        public float Acceleration { get; }
        public float Deceleration { get; }
        public float TurnSpeed { get; }
        public float GroundFriction { get; }
        public float AirControl { get; }
        public float Gravity { get; }
        public float JumpImpulse { get; }
        public float SlopeLimitDegrees { get; }
        public string LocomotionBlendId { get; }
    }

    public sealed class CharacterActionRequirement
    {
        public CharacterActionRequirement(CharacterActionRequirementKind kind, string stableId = "", int value = 0)
        {
            if (!Enum.IsDefined(typeof(CharacterActionRequirementKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind), "Requirement kind is not defined.");

            Kind = kind;
            StableId = stableId ?? string.Empty;
            Value = value;
        }

        public CharacterActionRequirementKind Kind { get; }
        public string StableId { get; }
        public int Value { get; }
    }

    public sealed class CharacterActionBinding
    {
        public CharacterActionBinding(
            string intentId,
            string actionId,
            int priority = 0,
            bool allowQueue = false,
            int queueWindowFrames = 0)
        {
            if (queueWindowFrames < 0)
                throw new ArgumentOutOfRangeException(nameof(queueWindowFrames), "Queue window frames cannot be negative.");

            IntentId = intentId ?? string.Empty;
            ActionId = actionId ?? string.Empty;
            Priority = priority;
            AllowQueue = allowQueue;
            QueueWindowFrames = queueWindowFrames;
        }

        public string IntentId { get; }
        public string ActionId { get; }
        public int Priority { get; }
        public bool AllowQueue { get; }
        public int QueueWindowFrames { get; }
    }

    public sealed class CharacterAbilityActionBinding
    {
        public CharacterAbilityActionBinding(
            int abilityId,
            string actionId,
            string[] requiredTags = null,
            string[] forbiddenTags = null)
        {
            AbilityId = abilityId;
            ActionId = actionId ?? string.Empty;
            RequiredTags = requiredTags ?? Array.Empty<string>();
            ForbiddenTags = forbiddenTags ?? Array.Empty<string>();
        }

        public int AbilityId { get; }
        public string ActionId { get; }
        public string[] RequiredTags { get; }
        public string[] ForbiddenTags { get; }
    }

    public sealed class CharacterReactionBinding
    {
        public CharacterReactionBinding(
            string reactionProfileId,
            string defaultActionId,
            string[] requiredTags = null,
            string[] forbiddenTags = null)
        {
            ReactionProfileId = reactionProfileId ?? string.Empty;
            DefaultActionId = defaultActionId ?? string.Empty;
            RequiredTags = requiredTags ?? Array.Empty<string>();
            ForbiddenTags = forbiddenTags ?? Array.Empty<string>();
        }

        public string ReactionProfileId { get; }
        public string DefaultActionId { get; }
        public string[] RequiredTags { get; }
        public string[] ForbiddenTags { get; }
    }

}

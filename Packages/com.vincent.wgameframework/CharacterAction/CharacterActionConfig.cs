using MxFramework.Runtime;
using System;

namespace MxFramework.CharacterAction
{
    [Flags]
    public enum CharacterActionRequestType
    {
        Any = 0,
        Idle = 1 << 0,
        Movement = 1 << 1,
        BasicAttack = 1 << 2,
        Skill = 1 << 3,
        Guard = 1 << 4,
        Dodge = 1 << 5,
        Jump = 1 << 6,
        Interaction = 1 << 7,
        Reaction = 1 << 8,
        PassiveOverlay = 1 << 9,
        All = Idle
            | Movement
            | BasicAttack
            | Skill
            | Guard
            | Dodge
            | Jump
            | Interaction
            | Reaction
            | PassiveOverlay,
    }

    [Flags]
    public enum CharacterCancelInputMask
    {
        Any = 0,
        AttackPrimary = 1 << 0,
        AttackSecondary = 1 << 1,
        Dodge = 1 << 2,
        Jump = 1 << 3,
        Skill = 1 << 4,
        Guard = 1 << 5,
        Interact = 1 << 6,
        LightLanding = 1 << 7,
        HeavyLanding = 1 << 8,
        All = AttackPrimary
            | AttackSecondary
            | Dodge
            | Jump
            | Skill
            | Guard
            | Interact
            | LightLanding
            | HeavyLanding,
    }

    [Obsolete("Use CharacterCancelInputMask to avoid confusion with tactical action slots.")]
    [Flags]
    public enum CharacterActionSlotType
    {
        Any = CharacterCancelInputMask.Any,
        AttackPrimary = CharacterCancelInputMask.AttackPrimary,
        AttackSecondary = CharacterCancelInputMask.AttackSecondary,
        Dodge = CharacterCancelInputMask.Dodge,
        Jump = CharacterCancelInputMask.Jump,
        Skill = CharacterCancelInputMask.Skill,
        Guard = CharacterCancelInputMask.Guard,
        Interact = CharacterCancelInputMask.Interact,
        LightLanding = CharacterCancelInputMask.LightLanding,
        HeavyLanding = CharacterCancelInputMask.HeavyLanding,
        All = CharacterCancelInputMask.All,
    }

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
        WeaponTypeRequired = 11,
        WeaponTypeIndependent = 12,
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
            int movementProfileId,
            string reactionProfileId,
            CharacterActionId defaultActionId,
            int defaultActionSlotMask = 0,
            string landingProfileId = "",
            CharacterActionCommandSequence[] commandSequences = null)
        {
            if (defaultActionSlotMask < 0)
                throw new ArgumentOutOfRangeException(nameof(defaultActionSlotMask), "Default action slot mask cannot be negative.");

            Id = id;
            StableId = stableId ?? string.Empty;
            var labelContent = new RuntimeContentHashAccumulator();
            labelContent.AddStringStable("label", StableId);
            StableLabelContentWitness = labelContent.ToHash();
            DisplayName = displayName ?? string.Empty;
            CharacterStableId = characterStableId ?? string.Empty;
            EquipmentStateStableId = equipmentStateStableId ?? string.Empty;
            CommandBindings = commandBindings ?? Array.Empty<CharacterActionBinding>();
            CommandSequences = commandSequences ?? Array.Empty<CharacterActionCommandSequence>();
            AbilityBindings = abilityBindings ?? Array.Empty<CharacterAbilityActionBinding>();
            ReactionBindings = reactionBindings ?? Array.Empty<CharacterReactionBinding>();
            MovementProfileId = movementProfileId;
            ReactionProfileId = reactionProfileId ?? string.Empty;
            DefaultActionId = defaultActionId;
            DefaultActionSlotMask = defaultActionSlotMask;
            LandingProfileId = landingProfileId ?? string.Empty;
        }

        public int Id { get; }
        public string StableId { get; }
        public long StableLabelContentWitness { get; }
        public string DisplayName { get; }
        public string CharacterStableId { get; }
        public string EquipmentStateStableId { get; }
        public CharacterActionBinding[] CommandBindings { get; }
        public CharacterActionCommandSequence[] CommandSequences { get; }
        public CharacterAbilityActionBinding[] AbilityBindings { get; }
        public CharacterReactionBinding[] ReactionBindings { get; }
        public int MovementProfileId { get; }
        public string ReactionProfileId { get; }
        public CharacterActionId DefaultActionId { get; }
        public int DefaultActionSlotMask { get; }
        public string LandingProfileId { get; }
    }

    public sealed class CharacterActionConfig
    {
        public CharacterActionConfig(
            CharacterActionId actionId,
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
            DebugTrackConfig debugTrack = null,
            CharacterActionCohesionSupportConfig cohesionSupport = null)
        {
            if (!Enum.IsDefined(typeof(CharacterActionCategory), category))
                throw new ArgumentOutOfRangeException(nameof(category), "Action category is not defined.");
            if (!Enum.IsDefined(typeof(CharacterActionTimelineAuthority), timelineAuthority))
                throw new ArgumentOutOfRangeException(nameof(timelineAuthority), "Timeline authority is not defined.");
            if (durationFrames.HasValue && durationFrames.Value < 0)
                throw new ArgumentOutOfRangeException(nameof(durationFrames), "Duration frames cannot be negative.");

            if (!actionId.IsValid)
                throw new ArgumentOutOfRangeException(nameof(actionId), "Character action id must be positive.");

            ActionId = actionId;
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
            CohesionSupport = cohesionSupport ?? CharacterActionCohesionSupportConfig.None;
        }

        public CharacterActionId ActionId { get; }
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
        public CharacterActionCohesionSupportConfig CohesionSupport { get; }
    }

    public sealed class CharacterActionCohesionSupportConfig
    {
        public static readonly CharacterActionCohesionSupportConfig None =
            new CharacterActionCohesionSupportConfig(false, 1f, 1f);

        public CharacterActionCohesionSupportConfig(
            bool enabled,
            float supportRate,
            float minSupportRatio,
            bool hasCohesionSupportWindow = false,
            int cohesionSupportWindowStartFrame = 0,
            int cohesionSupportWindowEndFrame = 0)
        {
            if (supportRate < 0f)
                throw new ArgumentOutOfRangeException(nameof(supportRate), "Cohesion support rate cannot be negative.");
            if (minSupportRatio < 0f)
                throw new ArgumentOutOfRangeException(nameof(minSupportRatio), "Cohesion support minimum ratio cannot be negative.");

            Enabled = enabled;
            SupportRate = supportRate <= 0f ? 1f : supportRate;
            MinSupportRatio = minSupportRatio <= 0f ? 1f : minSupportRatio;
            HasCohesionSupportWindow = hasCohesionSupportWindow;
            CohesionSupportWindowStartFrame = Math.Max(0, cohesionSupportWindowStartFrame);
            CohesionSupportWindowEndFrame = hasCohesionSupportWindow
                ? Math.Max(CohesionSupportWindowStartFrame, cohesionSupportWindowEndFrame)
                : 0;
        }

        public bool Enabled { get; }
        public float SupportRate { get; }
        public float MinSupportRatio { get; }
        public bool HasCohesionSupportWindow { get; }
        public int CohesionSupportWindowStartFrame { get; }
        public int CohesionSupportWindowEndFrame { get; }
    }

    public sealed class CharacterMovementProfileConfig
    {
        public CharacterMovementProfileConfig(
            int id,
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
            string locomotionBlendId,
            string stableId = "",
            float upwardGravityReference = 90f)
        {
            if (!Enum.IsDefined(typeof(CharacterMovementMode), defaultMode))
                throw new ArgumentOutOfRangeException(nameof(defaultMode), "Movement mode is not defined.");

            if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
            if (upwardGravityReference <= 0f
                || float.IsNaN(upwardGravityReference)
                || float.IsInfinity(upwardGravityReference))
                throw new ArgumentOutOfRangeException(nameof(upwardGravityReference));
            Id = id;
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
            UpwardGravityReference = upwardGravityReference;
            UpwardMotionGravityScaleMilli =
                CharacterActionVerticalRootMotionGravity.ResolveUpwardScaleMilli(
                    gravity, upwardGravityReference);
            JumpImpulse = jumpImpulse;
            SlopeLimitDegrees = slopeLimitDegrees;
            LocomotionBlendId = locomotionBlendId ?? string.Empty;
        }

        public int Id { get; }
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
        public float UpwardGravityReference { get; }
        public int UpwardMotionGravityScaleMilli { get; }
        public float JumpImpulse { get; }
        public float SlopeLimitDegrees { get; }
        public string LocomotionBlendId { get; }
    }

    public sealed class CharacterActionRequirement
    {
        public CharacterActionRequirement(CharacterActionRequirementKind kind, string stableId = "", int value = 0, int referenceId = 0)
        {
            if (!Enum.IsDefined(typeof(CharacterActionRequirementKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind), "Requirement kind is not defined.");

            Kind = kind;
            StableId = stableId ?? string.Empty;
            Value = value;
            if (referenceId < 0) throw new ArgumentOutOfRangeException(nameof(referenceId));
            ReferenceId = referenceId;
        }

        public CharacterActionRequirementKind Kind { get; }
        public string StableId { get; }
        public int Value { get; }
        public int ReferenceId { get; }

        public bool HasValidReferenceShape()
        {
            for (int i = 0; i < StableId.Length; i++) if (char.IsWhiteSpace(StableId[i])) return false;
            switch (Kind)
            {
                case CharacterActionRequirementKind.WeaponTypeIndependent:
                    return StableId.Length == 0 && Value == 0 && ReferenceId == 0;
                case CharacterActionRequirementKind.None:
                case CharacterActionRequirementKind.Grounded:
                case CharacterActionRequirementKind.Airborne:
                case CharacterActionRequirementKind.TargetValid:
                    return ReferenceId == 0 && Value == 0 && StableId.Length == 0;
                case CharacterActionRequirementKind.WeaponTypeRequired:
                    return StableId.Length > 0 && Value > 0 && ReferenceId == 0;
                case CharacterActionRequirementKind.ResourceAvailable:
                    return ReferenceId > 0 && Value >= 0;
                case CharacterActionRequirementKind.CooldownReady:
                case CharacterActionRequirementKind.EquipmentTagRequired:
                case CharacterActionRequirementKind.EquipmentTagForbidden:
                    return ReferenceId > 0 && Value == 0;
                default:
                    return ReferenceId >= 0;
            }
        }
    }

    public sealed class CharacterActionBinding
    {
        public CharacterActionBinding(
            int intentId,
            CharacterActionId actionId,
            int priority = 0,
            bool allowQueue = false,
            int queueWindowFrames = 0)
        {
            if (queueWindowFrames < 0)
                throw new ArgumentOutOfRangeException(nameof(queueWindowFrames), "Queue window frames cannot be negative.");

            IntentId = intentId;
            ActionId = actionId;
            Priority = priority;
            AllowQueue = allowQueue;
            QueueWindowFrames = queueWindowFrames;
        }

        public int IntentId { get; }
        public CharacterActionId ActionId { get; }
        public int Priority { get; }
        public bool AllowQueue { get; }
        public int QueueWindowFrames { get; }
    }

    /// <summary>
    /// Defines an ordered command combo. The resolver advances from the currently active action
    /// while the runner remains authoritative for transition windows and queued execution.
    /// </summary>
    public sealed class CharacterActionCommandSequence
    {
        public CharacterActionCommandSequence(
            int intentId,
            CharacterActionId[] actionIds,
            bool wrapAfterLast = false,
            int priority = 0,
            bool allowQueue = false,
            int queueWindowFrames = 0)
        {
            if (queueWindowFrames < 0)
                throw new ArgumentOutOfRangeException(nameof(queueWindowFrames), "Queue window frames cannot be negative.");

            IntentId = intentId;
            ActionIds = actionIds ?? Array.Empty<CharacterActionId>();
            WrapAfterLast = wrapAfterLast;
            Priority = priority;
            AllowQueue = allowQueue;
            QueueWindowFrames = queueWindowFrames;
        }

        public int IntentId { get; }
        public CharacterActionId[] ActionIds { get; }
        public bool WrapAfterLast { get; }
        public int Priority { get; }
        public bool AllowQueue { get; }
        public int QueueWindowFrames { get; }

    }

    public sealed class CharacterAbilityActionBinding
    {
        public CharacterAbilityActionBinding(
            int abilityId,
            CharacterActionId actionId,
            string[] requiredTags = null,
            string[] forbiddenTags = null)
        {
            AbilityId = abilityId;
            ActionId = actionId;
            RequiredTags = requiredTags ?? Array.Empty<string>();
            ForbiddenTags = forbiddenTags ?? Array.Empty<string>();
        }

        public int AbilityId { get; }
        public CharacterActionId ActionId { get; }
        public string[] RequiredTags { get; }
        public string[] ForbiddenTags { get; }
    }

    public sealed class CharacterReactionBinding
    {
        public CharacterReactionBinding(
            string reactionProfileId,
            CharacterActionId defaultActionId,
            string[] requiredTags = null,
            string[] forbiddenTags = null)
        {
            ReactionProfileId = reactionProfileId ?? string.Empty;
            DefaultActionId = defaultActionId;
            RequiredTags = requiredTags ?? Array.Empty<string>();
            ForbiddenTags = forbiddenTags ?? Array.Empty<string>();
        }

        public string ReactionProfileId { get; }
        public CharacterActionId DefaultActionId { get; }
        public string[] RequiredTags { get; }
        public string[] ForbiddenTags { get; }
    }

}

using System;

namespace MxFramework.CharacterAction
{
    public enum CharacterActionTrackKind
    {
        Motion = 0,
        Combat = 1,
        Gameplay = 2,
        Animation = 3,
        Presentation = 4,
        Debug = 5,
    }

    public enum CharacterActionTrackEventKind
    {
        None = 0,
        SetMovementMode = 1,
        ApplyImpulse = 2,
        LockMovement = 3,
        StartCombatAction = 100,
        StartHitTrace = 101,
        StopHitTrace = 102,
        SendGameplayRequest = 200,
        CastAbility = 201,
        ApplyGameplayEffect = 202,
        PlayAnimation = 300,
        CrossFadeAnimation = 301,
        SetAnimationBlend = 302,
        PlayAudioCue = 400,
        SpawnVisualCue = 401,
        CameraImpulse = 402,
        UiFeedback = 403,
        EmitDebugMarker = 500,
    }

    public sealed class MotionTrackConfig
    {
        public static readonly MotionTrackConfig Empty = new MotionTrackConfig(usesRootMotion: false, events: null);

        public MotionTrackConfig(bool usesRootMotion, MotionTrackEvent[] events)
        {
            UsesRootMotion = usesRootMotion;
            Events = events ?? Array.Empty<MotionTrackEvent>();
        }

        public bool UsesRootMotion { get; }
        public MotionTrackEvent[] Events { get; }
    }

    public sealed class CombatTrackConfig
    {
        public static readonly CombatTrackConfig Empty = new CombatTrackConfig(string.Empty, null);

        public CombatTrackConfig(string combatActionId, CombatTrackEvent[] events)
        {
            CombatActionId = combatActionId ?? string.Empty;
            Events = events ?? Array.Empty<CombatTrackEvent>();
        }

        public string CombatActionId { get; }
        public CombatTrackEvent[] Events { get; }
    }

    public sealed class GameplayTrackConfig
    {
        public static readonly GameplayTrackConfig Empty = new GameplayTrackConfig(null);

        public GameplayTrackConfig(GameplayTrackEvent[] events)
        {
            Events = events ?? Array.Empty<GameplayTrackEvent>();
        }

        public GameplayTrackEvent[] Events { get; }
    }

    public sealed class AnimationTrackConfig
    {
        public static readonly AnimationTrackConfig Empty = new AnimationTrackConfig(null);

        public AnimationTrackConfig(AnimationTrackEvent[] events)
        {
            Events = events ?? Array.Empty<AnimationTrackEvent>();
        }

        public AnimationTrackEvent[] Events { get; }
    }

    public sealed class PresentationTrackConfig
    {
        public static readonly PresentationTrackConfig Empty = new PresentationTrackConfig(null);

        public PresentationTrackConfig(PresentationTrackEvent[] events)
        {
            Events = events ?? Array.Empty<PresentationTrackEvent>();
        }

        public PresentationTrackEvent[] Events { get; }
    }

    public sealed class DebugTrackConfig
    {
        public static readonly DebugTrackConfig Empty = new DebugTrackConfig(null);

        public DebugTrackConfig(DebugTrackEvent[] events)
        {
            Events = events ?? Array.Empty<DebugTrackEvent>();
        }

        public DebugTrackEvent[] Events { get; }
    }

    public readonly struct MotionTrackEvent
    {
        public MotionTrackEvent(
            int frame,
            CharacterActionTrackEventKind kind,
            CharacterMovementMode movementMode = CharacterMovementMode.Idle,
            float x = 0f,
            float y = 0f,
            float z = 0f,
            string stableEventId = "")
        {
            CharacterActionTrackEventValidation.ValidateFrameAndKind(frame, kind, CharacterActionTrackKind.Motion);
            if (!Enum.IsDefined(typeof(CharacterMovementMode), movementMode))
                throw new ArgumentOutOfRangeException(nameof(movementMode), "Movement mode is not defined.");

            Frame = frame;
            Kind = kind;
            MovementMode = movementMode;
            X = x;
            Y = y;
            Z = z;
            StableEventId = stableEventId ?? string.Empty;
        }

        public int Frame { get; }
        public CharacterActionTrackEventKind Kind { get; }
        public CharacterMovementMode MovementMode { get; }
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public string StableEventId { get; }
    }

    public readonly struct CombatTrackEvent
    {
        public CombatTrackEvent(int frame, CharacterActionTrackEventKind kind, string combatActionId = "", string traceProfileId = "", string stableEventId = "")
        {
            CharacterActionTrackEventValidation.ValidateFrameAndKind(frame, kind, CharacterActionTrackKind.Combat);
            Frame = frame;
            Kind = kind;
            CombatActionId = combatActionId ?? string.Empty;
            TraceProfileId = traceProfileId ?? string.Empty;
            StableEventId = stableEventId ?? string.Empty;
        }

        public int Frame { get; }
        public CharacterActionTrackEventKind Kind { get; }
        public string CombatActionId { get; }
        public string TraceProfileId { get; }
        public string StableEventId { get; }
    }

    public readonly struct GameplayTrackEvent
    {
        public GameplayTrackEvent(int frame, CharacterActionTrackEventKind kind, string requestId = "", string abilityStableId = "", string stableEventId = "")
        {
            CharacterActionTrackEventValidation.ValidateFrameAndKind(frame, kind, CharacterActionTrackKind.Gameplay);
            Frame = frame;
            Kind = kind;
            RequestId = requestId ?? string.Empty;
            AbilityStableId = abilityStableId ?? string.Empty;
            StableEventId = stableEventId ?? string.Empty;
        }

        public int Frame { get; }
        public CharacterActionTrackEventKind Kind { get; }
        public string RequestId { get; }
        public string AbilityStableId { get; }
        public string StableEventId { get; }
    }

    public readonly struct AnimationTrackEvent
    {
        public AnimationTrackEvent(int frame, CharacterActionTrackEventKind kind, string animationActionKey = "", float transitionSeconds = 0f, string stableEventId = "")
        {
            CharacterActionTrackEventValidation.ValidateFrameAndKind(frame, kind, CharacterActionTrackKind.Animation);
            Frame = frame;
            Kind = kind;
            AnimationActionKey = animationActionKey ?? string.Empty;
            TransitionSeconds = transitionSeconds;
            StableEventId = stableEventId ?? string.Empty;
        }

        public int Frame { get; }
        public CharacterActionTrackEventKind Kind { get; }
        public string AnimationActionKey { get; }
        public float TransitionSeconds { get; }
        public string StableEventId { get; }
    }

    public readonly struct PresentationTrackEvent
    {
        public PresentationTrackEvent(int frame, CharacterActionTrackEventKind kind, string cueId = "", string resourceKey = "", string stableEventId = "")
        {
            CharacterActionTrackEventValidation.ValidateFrameAndKind(frame, kind, CharacterActionTrackKind.Presentation);
            Frame = frame;
            Kind = kind;
            CueId = cueId ?? string.Empty;
            ResourceKey = resourceKey ?? string.Empty;
            StableEventId = stableEventId ?? string.Empty;
        }

        public int Frame { get; }
        public CharacterActionTrackEventKind Kind { get; }
        public string CueId { get; }
        public string ResourceKey { get; }
        public string StableEventId { get; }
    }

    public readonly struct DebugTrackEvent
    {
        public DebugTrackEvent(int frame, CharacterActionTrackEventKind kind, string markerId = "", string stableEventId = "")
        {
            CharacterActionTrackEventValidation.ValidateFrameAndKind(frame, kind, CharacterActionTrackKind.Debug);
            Frame = frame;
            Kind = kind;
            MarkerId = markerId ?? string.Empty;
            StableEventId = stableEventId ?? string.Empty;
        }

        public int Frame { get; }
        public CharacterActionTrackEventKind Kind { get; }
        public string MarkerId { get; }
        public string StableEventId { get; }
    }

    internal static class CharacterActionTrackEventValidation
    {
        public static void ValidateFrameAndKind(int frame, CharacterActionTrackEventKind kind, CharacterActionTrackKind trackKind)
        {
            if (frame < 0)
                throw new ArgumentOutOfRangeException(nameof(frame), "Track event frame cannot be negative.");
            if (!Enum.IsDefined(typeof(CharacterActionTrackEventKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind), "Track event kind is not defined.");
            if (!Enum.IsDefined(typeof(CharacterActionTrackKind), trackKind))
                throw new ArgumentOutOfRangeException(nameof(trackKind), "Track kind is not defined.");
            if (!BelongsToTrack(kind, trackKind))
                throw new ArgumentOutOfRangeException(nameof(kind), "Track event kind does not belong to the requested track.");
        }

        private static bool BelongsToTrack(CharacterActionTrackEventKind kind, CharacterActionTrackKind trackKind)
        {
            switch (trackKind)
            {
                case CharacterActionTrackKind.Motion:
                    return kind >= CharacterActionTrackEventKind.SetMovementMode && kind < CharacterActionTrackEventKind.StartCombatAction;
                case CharacterActionTrackKind.Combat:
                    return kind >= CharacterActionTrackEventKind.StartCombatAction && kind < CharacterActionTrackEventKind.SendGameplayRequest;
                case CharacterActionTrackKind.Gameplay:
                    return kind >= CharacterActionTrackEventKind.SendGameplayRequest && kind < CharacterActionTrackEventKind.PlayAnimation;
                case CharacterActionTrackKind.Animation:
                    return kind >= CharacterActionTrackEventKind.PlayAnimation && kind < CharacterActionTrackEventKind.PlayAudioCue;
                case CharacterActionTrackKind.Presentation:
                    return kind >= CharacterActionTrackEventKind.PlayAudioCue && kind < CharacterActionTrackEventKind.EmitDebugMarker;
                case CharacterActionTrackKind.Debug:
                    return kind >= CharacterActionTrackEventKind.EmitDebugMarker;
                default:
                    return false;
            }
        }
    }
}

using System;
using MxFramework.Combat.Damage;
using MxFramework.Combat.Hit;
using MxFramework.Core;
using MxFramework.Runtime;

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
        SetControlScale = 4,
        SetCollisionProfile = 6,
        SetVerticalVelocity = 7,
        IgnoreGravity = 8,
        SampleRootMotionY = 9,
        StartCombatAction = 100,
        HitTraceWindow = 101,
        SetHitTargetState = 102,
        SendGameplayRequest = 200,
        CastAbility = 201,
        ApplyGameplayEffect = 202,
        StartEffectTimeline = 203,
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
            : this(usesRootMotion, events, rootMotion: null)
        {
        }

        public MotionTrackConfig(
            bool usesRootMotion,
            MotionTrackEvent[] events,
            CharacterActionRootMotionConfig rootMotion,
            CharacterActionVerticalRootMotionConfig verticalRootMotion = null)
        {
            UsesRootMotion = usesRootMotion;
            Events = events ?? Array.Empty<MotionTrackEvent>();
            RootMotion = rootMotion;
            VerticalRootMotion = verticalRootMotion;
        }

        public bool UsesRootMotion { get; }
        public MotionTrackEvent[] Events { get; }
        public CharacterActionRootMotionConfig RootMotion { get; }
        public CharacterActionVerticalRootMotionConfig VerticalRootMotion { get; }

        public bool IsVerticalRootMotionActive(int frame)
        {
            for (int i = 0; i < Events.Length; i++)
            {
                MotionTrackEvent evt = Events[i];
                if (evt.Kind == CharacterActionTrackEventKind.SampleRootMotionY
                    && evt.Frame <= frame && frame <= evt.EndFrame)
                    return true;
            }
            return false;
        }

        public bool TryGetVerticalRootMotionDelta(int frame, out float y)
        {
            y = 0f;
            return IsVerticalRootMotionActive(frame)
                && VerticalRootMotion != null
                && VerticalRootMotion.TryGetDelta(frame, out y);
        }
    }

    public enum CharacterActionRootMotionSpace
    {
        Local = 0,
        World = 1,
    }

    public enum CharacterActionRootMotionObstaclePolicy
    {
        Default = 0,
        StaticOnly = 1,
    }

    public enum CharacterActionMotionVectorSpace
    {
        World = 0,
        Local = 1,
    }

    public enum CharacterActionCollisionProfileRestorePolicy
    {
        None = 0,
        OnActionEnd = 1,
    }

    public sealed class CharacterActionRootMotionConfig
    {
        public static readonly CharacterActionRootMotionConfig Empty =
            new CharacterActionRootMotionConfig(
                0,
                string.Empty,
                CharacterActionRootMotionSpace.Local,
                CharacterActionAnimationKeyUtility.TimelineFramesPerSecond,
                null);

        private readonly CharacterActionRootMotionFrame[] _frames;

        public CharacterActionRootMotionConfig(
            int sourceAnimationEventId,
            string sourceAnimationClipResourceKey,
            CharacterActionRootMotionSpace space,
            int sampleTickRate,
            CharacterActionRootMotionFrame[] frames)
            : this(
                sourceAnimationEventId,
                sourceAnimationClipResourceKey,
                space,
                sampleTickRate,
                CharacterActionRootMotionObstaclePolicy.Default,
                frames)
        {
        }

        public CharacterActionRootMotionConfig(
            int sourceAnimationEventId,
            string sourceAnimationClipResourceKey,
            CharacterActionRootMotionSpace space,
            int sampleTickRate,
            CharacterActionRootMotionObstaclePolicy obstaclePolicy,
            CharacterActionRootMotionFrame[] frames)
        {
            if (!Enum.IsDefined(typeof(CharacterActionRootMotionSpace), space))
                throw new ArgumentOutOfRangeException(nameof(space), "Root motion space is not defined.");
            if (!Enum.IsDefined(typeof(CharacterActionRootMotionObstaclePolicy), obstaclePolicy))
                throw new ArgumentOutOfRangeException(nameof(obstaclePolicy), "Root motion obstacle policy is not defined.");

            SourceAnimationEventId = sourceAnimationEventId;
            SourceAnimationClipResourceKey = sourceAnimationClipResourceKey ?? string.Empty;
            Space = space;
            SampleTickRate = sampleTickRate <= 0
                ? CharacterActionAnimationKeyUtility.TimelineFramesPerSecond
                : sampleTickRate;
            ObstaclePolicy = obstaclePolicy;
            _frames = CopyAndSortFrames(frames);
            ContentWitnessHash = ComputeContentWitness();
        }

        public int SourceAnimationEventId { get; }
        public string SourceAnimationClipResourceKey { get; }
        public CharacterActionRootMotionSpace Space { get; }
        public int SampleTickRate { get; }
        public CharacterActionRootMotionObstaclePolicy ObstaclePolicy { get; }
        public ReadOnlySpan<CharacterActionRootMotionFrame> Frames => _frames;
        public long ContentWitnessHash { get; }
        public bool HasFrames => _frames.Length > 0;
        public int StartFrame => HasFrames ? _frames[0].Frame : 0;
        public int EndFrame => HasFrames ? _frames[_frames.Length - 1].Frame : 0;

        private long ComputeContentWitness()
        {
            var hash = new RuntimeContentHashAccumulator();
            hash.AddInt("sourceEvent", SourceAnimationEventId);
            hash.AddStringStable("sourceClip", SourceAnimationClipResourceKey);
            hash.AddInt("space", (int)Space);
            hash.AddInt("sampleRate", SampleTickRate);
            hash.AddInt("obstaclePolicy", (int)ObstaclePolicy);
            hash.AddInt("frames", _frames.Length);
            for (int i = 0; i < _frames.Length; i++)
            {
                CharacterActionRootMotionFrame value = _frames[i];
                hash.AddInt("frame", value.Frame);
                hash.AddInt("x", BitConverter.SingleToInt32Bits(value.X));
                hash.AddInt("y", BitConverter.SingleToInt32Bits(value.Y));
                hash.AddInt("z", BitConverter.SingleToInt32Bits(value.Z));
            }
            return hash.ToHash();
        }

        public bool TryGetDelta(int frame, out float x, out float y, out float z)
        {
            for (int i = 0; i < _frames.Length; i++)
            {
                CharacterActionRootMotionFrame candidate = _frames[i];
                if (candidate.Frame != frame)
                    continue;

                x = candidate.X;
                y = candidate.Y;
                z = candidate.Z;
                return true;
            }

            x = 0f;
            y = 0f;
            z = 0f;
            return false;
        }

        private static CharacterActionRootMotionFrame[] CopyAndSortFrames(CharacterActionRootMotionFrame[] frames)
        {
            if (frames == null || frames.Length == 0)
                return Array.Empty<CharacterActionRootMotionFrame>();

            var copy = new CharacterActionRootMotionFrame[frames.Length];
            Array.Copy(frames, copy, frames.Length);
            Array.Sort(copy, CompareFrames);
            return copy;
        }

        private static int CompareFrames(CharacterActionRootMotionFrame left, CharacterActionRootMotionFrame right)
        {
            return left.Frame.CompareTo(right.Frame);
        }
    }

    public readonly struct CharacterActionRootMotionFrame
    {
        public CharacterActionRootMotionFrame(int frame, float x, float y, float z)
        {
            if (frame < 0)
                throw new ArgumentOutOfRangeException(nameof(frame), "Root motion frame cannot be negative.");

            Frame = frame;
            X = x;
            Y = 0f;
            Z = z;
        }

        public int Frame { get; }
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
    }

    public sealed class CharacterActionVerticalRootMotionConfig
    {
        private readonly CharacterActionVerticalRootMotionFrame[] _frames;

        public CharacterActionVerticalRootMotionConfig(
            int sourceAnimationEventId,
            string sourceAnimationClipResourceKey,
            CharacterActionVerticalRootMotionFrame[] frames)
        {
            SourceAnimationEventId = sourceAnimationEventId;
            SourceAnimationClipResourceKey = sourceAnimationClipResourceKey ?? string.Empty;
            _frames = frames ?? Array.Empty<CharacterActionVerticalRootMotionFrame>();
            var hash = new RuntimeContentHashAccumulator();
            hash.AddInt("sourceEvent", SourceAnimationEventId);
            hash.AddStringStable("sourceClip", SourceAnimationClipResourceKey);
            hash.AddInt("frames", _frames.Length);
            for (int i = 0; i < _frames.Length; i++)
            {
                hash.AddInt("frame", _frames[i].Frame);
                hash.AddInt("y", BitConverter.SingleToInt32Bits(_frames[i].Y));
            }
            ContentWitnessHash = hash.ToHash();
        }

        public int SourceAnimationEventId { get; }
        public string SourceAnimationClipResourceKey { get; }
        public bool HasFrames => _frames.Length > 0;
        public long ContentWitnessHash { get; }

        public bool TryGetDelta(int frame, out float y)
        {
            for (int i = 0; i < _frames.Length; i++)
            {
                if (_frames[i].Frame != frame)
                    continue;
                y = _frames[i].Y;
                return true;
            }
            y = 0f;
            return false;
        }
    }

    public readonly struct CharacterActionVerticalRootMotionFrame
    {
        public CharacterActionVerticalRootMotionFrame(int frame, float y)
        {
            if (frame < 0)
                throw new ArgumentOutOfRangeException(nameof(frame));
            Frame = frame;
            Y = y;
        }

        public int Frame { get; }
        public float Y { get; }
    }

    /// <summary>
    /// Converts gravity into a positive multiplier for an upward authored Y sample.
    /// The gravity contribution for one tick can reduce the sample, but never reverse it.
    /// </summary>
    public static class CharacterActionVerticalRootMotionGravity
    {
        public const int ScaleMilli = 1000;
        public const int MinimumUpwardScaleMilli = 10;
        public const int ReferenceGravityMillimetersPerSecondSquared = 90000;

        public static int ResolveUpwardScaleMilli(
            int gravityMillimetersPerSecondSquared,
            int referenceGravityMillimetersPerSecondSquared =
                ReferenceGravityMillimetersPerSecondSquared)
        {
            if (gravityMillimetersPerSecondSquared == 0)
                return ScaleMilli;

            long gravity = Math.Abs((long)gravityMillimetersPerSecondSquared);
            long reference = Math.Max(1, referenceGravityMillimetersPerSecondSquared);
            long denominator = reference + gravity;
            long scale = (reference * ScaleMilli
                + denominator / 2) / denominator;
            return (int)Math.Max(MinimumUpwardScaleMilli, Math.Min(ScaleMilli, scale));
        }

        public static int ResolveUpwardScaleMilli(
            float gravityMetersPerSecondSquared,
            float referenceGravityMetersPerSecondSquared = 90f)
        {
            if (referenceGravityMetersPerSecondSquared <= 0f
                || float.IsNaN(referenceGravityMetersPerSecondSquared)
                || float.IsInfinity(referenceGravityMetersPerSecondSquared))
                throw new ArgumentOutOfRangeException(nameof(referenceGravityMetersPerSecondSquared));
            double millimeters = Math.Abs((double)gravityMetersPerSecondSquared) * 1000d;
            if (double.IsNaN(millimeters) || double.IsInfinity(millimeters))
                return MinimumUpwardScaleMilli;
            int quantized = (int)Math.Min(int.MaxValue, Math.Round(millimeters));
            double reference = Math.Max(0.001d, referenceGravityMetersPerSecondSquared) * 1000d;
            int referenceQuantized = (int)Math.Min(int.MaxValue, Math.Round(reference));
            return ResolveUpwardScaleMilli(quantized, referenceQuantized);
        }
    }

    public sealed class CombatTrackConfig
    {
        public static readonly CombatTrackConfig Empty = new CombatTrackConfig(0, null);

        public CombatTrackConfig(int combatActionId, CombatTrackEvent[] events)
        {
            CombatActionId = combatActionId;
            Events = events ?? Array.Empty<CombatTrackEvent>();
        }

        public int CombatActionId { get; }
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

    public readonly struct CharacterActionDamagePayload : IEquatable<CharacterActionDamagePayload>
    {
        public static readonly CharacterActionDamagePayload Empty = new CharacterActionDamagePayload();

        public CharacterActionDamagePayload(
            int damageTypeId = 0,
            int formulaId = 0,
            int baseDamage = 0,
            DamageAttributeScale[] sourceAttributeScales = null,
            int impactPower = 0,
            int[] tags = null,
            int analysisCategory = 0,
            int presentationProfileId = 0,
            int launchPower = 0,
            string damageTypeLabel = "", string formulaLabel = "", string analysisCategoryLabel = "",
            string presentationProfileLabel = "", string[] tagLabels = null)
        {
            if (damageTypeId < 0 || formulaId < 0 || analysisCategory < 0 || presentationProfileId < 0)
                throw new ArgumentOutOfRangeException(nameof(damageTypeId), "Numeric content identities cannot be negative.");
            DamageTypeLabel = damageTypeLabel ?? string.Empty; FormulaLabel = formulaLabel ?? string.Empty;
            AnalysisCategoryLabel = analysisCategoryLabel ?? string.Empty; PresentationProfileLabel = presentationProfileLabel ?? string.Empty;
            TagLabels = tagLabels == null ? Array.Empty<string>() : (string[])tagLabels.Clone();
            DamageTypeId = damageTypeId;
            FormulaId = formulaId;
            BaseDamage = baseDamage < 0 ? 0 : baseDamage;
            SourceAttributeScales = Copy(sourceAttributeScales);
            ImpactPower = impactPower < 0 ? 0 : impactPower;
            LaunchPower = launchPower < 0 ? 0 : launchPower;
            Tags = Copy(tags);
            AnalysisCategory = analysisCategory;
            PresentationProfileId = presentationProfileId;
        }

        public string DamageTypeLabel { get; }
        public string FormulaLabel { get; }
        public string AnalysisCategoryLabel { get; }
        public string PresentationProfileLabel { get; }
        public string[] TagLabels { get; }
        public int DamageTypeId { get; }
        public int FormulaId { get; }
        public int BaseDamage { get; }
        public DamageAttributeScale[] SourceAttributeScales { get; }
        public int ImpactPower { get; }
        public int LaunchPower { get; }
        public int[] Tags { get; }
        public int AnalysisCategory { get; }
        public int PresentationProfileId { get; }
        public bool HasInlineData =>
            DamageTypeId > 0
            || FormulaId > 0
            || BaseDamage > 0
            || (SourceAttributeScales != null && SourceAttributeScales.Length > 0)
            || ImpactPower > 0
            || LaunchPower > 0
            || (Tags != null && Tags.Length > 0)
            || AnalysisCategory > 0;

        public bool Equals(CharacterActionDamagePayload other)
        {
            return DamageTypeId == other.DamageTypeId
                && FormulaId == other.FormulaId
                && BaseDamage == other.BaseDamage
                && ImpactPower == other.ImpactPower
                && LaunchPower == other.LaunchPower
                && AnalysisCategory == other.AnalysisCategory
                && PresentationProfileId == other.PresentationProfileId
                && SequenceEquals(SourceAttributeScales, other.SourceAttributeScales)
                && SequenceEquals(Tags, other.Tags);
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterActionDamagePayload other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = DamageTypeId;
                hash = (hash * 397) ^ FormulaId;
                hash = (hash * 397) ^ BaseDamage;
                hash = (hash * 397) ^ ImpactPower;
                hash = (hash * 397) ^ LaunchPower;
                hash = (hash * 397) ^ AnalysisCategory;
                hash = (hash * 397) ^ PresentationProfileId;
                hash = AppendHash(hash, SourceAttributeScales);
                hash = AppendHash(hash, Tags);
                return hash;
            }
        }

        private static DamageAttributeScale[] Copy(DamageAttributeScale[] values)
        {
            if (values == null || values.Length == 0)
                return Array.Empty<DamageAttributeScale>();

            var copy = new DamageAttributeScale[values.Length];
            Array.Copy(values, copy, values.Length);
            return copy;
        }

        private static int[] Copy(int[] values)
        {
            if (values == null || values.Length == 0)
                return Array.Empty<int>();

            var copy = new int[values.Length];
            Array.Copy(values, copy, values.Length);
            return copy;
        }

        private static bool SequenceEquals(DamageAttributeScale[] left, DamageAttributeScale[] right)
        {
            left = left ?? Array.Empty<DamageAttributeScale>();
            right = right ?? Array.Empty<DamageAttributeScale>();
            if (left.Length != right.Length)
                return false;

            for (int i = 0; i < left.Length; i++)
            {
                if (!left[i].Equals(right[i]))
                    return false;
            }

            return true;
        }

        private static bool SequenceEquals(int[] left, int[] right)
        {
            left = left ?? Array.Empty<int>();
            right = right ?? Array.Empty<int>();
            if (left.Length != right.Length)
                return false;

            for (int i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i])
                    return false;
            }

            return true;
        }

        private static int AppendHash(int hash, DamageAttributeScale[] values)
        {
            values = values ?? Array.Empty<DamageAttributeScale>();
            for (int i = 0; i < values.Length; i++)
                hash = (hash * 397) ^ values[i].GetHashCode();
            return hash;
        }

        private static int AppendHash(int hash, int[] values)
        {
            values = values ?? Array.Empty<int>();
            for (int i = 0; i < values.Length; i++)
                hash = (hash * 397) ^ values[i];
            return hash;
        }
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
            int stableEventId = 0,
            float movementControlScale = 1f,
            float rotationControlScale = 1f,
            CharacterActionMotionVectorSpace space = CharacterActionMotionVectorSpace.World,
            bool hasMovementControlScale = true,
            bool hasRotationControlScale = true,
            int collisionProfileId = 0,
            CharacterActionCollisionProfileRestorePolicy collisionRestorePolicy =
                CharacterActionCollisionProfileRestorePolicy.OnActionEnd,
            int collisionTransitionFrames = 0,
            int endFrame = -1,
            int stableEventOrder = 0)
        {
            CharacterActionTrackEventValidation.ValidateFrameAndKind(frame, kind, CharacterActionTrackKind.Motion);
            if (!Enum.IsDefined(typeof(CharacterMovementMode), movementMode))
                throw new ArgumentOutOfRangeException(nameof(movementMode), "Movement mode is not defined.");
            if (!Enum.IsDefined(typeof(CharacterActionMotionVectorSpace), space))
                throw new ArgumentOutOfRangeException(nameof(space), "Motion vector space is not defined.");
            if (!Enum.IsDefined(typeof(CharacterActionCollisionProfileRestorePolicy), collisionRestorePolicy))
                throw new ArgumentOutOfRangeException(nameof(collisionRestorePolicy), "Collision profile restore policy is not defined.");

            Frame = frame;
            Kind = kind;
            MovementMode = movementMode;
            X = x;
            Y = y;
            Z = z;
            StableEventId = stableEventId;
            StableEventOrder = stableEventOrder;
            MovementControlScale = Clamp01(movementControlScale);
            RotationControlScale = Clamp01(rotationControlScale);
            Space = space;
            HasMovementControlScale = hasMovementControlScale;
            HasRotationControlScale = hasRotationControlScale;
            CollisionProfileId = collisionProfileId;
            CollisionRestorePolicy = collisionRestorePolicy;
            CollisionTransitionFrames = collisionTransitionFrames < 0 ? 0 : collisionTransitionFrames;
            EndFrame = endFrame < 0 ? frame : Math.Max(frame, endFrame);
        }

        public int Frame { get; }
        public CharacterActionTrackEventKind Kind { get; }
        public CharacterMovementMode MovementMode { get; }
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public int StableEventId { get; }
        public int StableEventOrder { get; }
        public float MovementControlScale { get; }
        public float RotationControlScale { get; }
        public CharacterActionMotionVectorSpace Space { get; }
        public bool HasMovementControlScale { get; }
        public bool HasRotationControlScale { get; }
        public int CollisionProfileId { get; }
        public CharacterActionCollisionProfileRestorePolicy CollisionRestorePolicy { get; }
        public int CollisionTransitionFrames { get; }
        public int EndFrame { get; }

        private static float Clamp01(float value)
        {
            if (value <= 0f)
                return 0f;
            return value >= 1f ? 1f : value;
        }
    }

    public enum CharacterActionHitVolumeShapeKind
    {
        None = 0,
        Capsule = 1,
    }

    public enum CharacterActionHitVolumeSourceKind
    {
        ManualCapsule = 0,
        CasterBody = 1,
        BakedSocketCapsule = 2,
    }

    /// <summary>
    /// Optional authoring-time hit volume for combat track traces.
    /// </summary>
    /// <remarks>
    /// NoAlloc value type. Missing manual capsule data is treated as no hit volume;
    /// CasterBody derives the volume from the runtime character physics body.
    /// </remarks>
    public readonly struct CharacterActionHitVolumeConfig : IEquatable<CharacterActionHitVolumeConfig>
    {
        public CharacterActionHitVolumeConfig(
            CharacterActionHitVolumeShapeKind shapeKind,
            float localPointAX,
            float localPointAY,
            float localPointAZ,
            float localPointBX,
            float localPointBY,
            float localPointBZ,
            float radius)
            : this(
                CharacterActionHitVolumeSourceKind.ManualCapsule,
                shapeKind,
                localPointAX,
                localPointAY,
                localPointAZ,
                localPointBX,
                localPointBY,
                localPointBZ,
                radius)
        {
        }

        public CharacterActionHitVolumeConfig(
            CharacterActionHitVolumeSourceKind sourceKind,
            CharacterActionHitVolumeShapeKind shapeKind,
            float localPointAX,
            float localPointAY,
            float localPointAZ,
            float localPointBX,
            float localPointBY,
            float localPointBZ,
            float radius)
            : this(
                sourceKind,
                shapeKind,
                localPointAX,
                localPointAY,
                localPointAZ,
                localPointBX,
                localPointBY,
                localPointBZ,
                radius,
                0f,
                0f,
                0f,
                1f,
                0f,
                1f,
                0f)
        {
        }

        public CharacterActionHitVolumeConfig(
            CharacterActionHitVolumeSourceKind sourceKind,
            CharacterActionHitVolumeShapeKind shapeKind,
            float localPointAX,
            float localPointAY,
            float localPointAZ,
            float localPointBX,
            float localPointBY,
            float localPointBZ,
            float radius,
            float bodyOffsetX,
            float bodyOffsetY,
            float bodyOffsetZ,
            float bodyRadiusScale,
            float bodyRadiusAdd,
            float bodyHeightScale,
            float bodyHeightAdd)
            : this(
                sourceKind,
                shapeKind,
                localPointAX,
                localPointAY,
                localPointAZ,
                localPointBX,
                localPointBY,
                localPointBZ,
                radius,
                bodyOffsetX,
                bodyOffsetY,
                bodyOffsetZ,
                bodyRadiusScale,
                bodyRadiusAdd,
                bodyHeightScale,
                bodyHeightAdd,
                0,
                0)
        {
        }

        private CharacterActionHitVolumeConfig(
            CharacterActionHitVolumeSourceKind sourceKind,
            CharacterActionHitVolumeShapeKind shapeKind,
            float localPointAX,
            float localPointAY,
            float localPointAZ,
            float localPointBX,
            float localPointBY,
            float localPointBZ,
            float radius,
            float bodyOffsetX,
            float bodyOffsetY,
            float bodyOffsetZ,
            float bodyRadiusScale,
            float bodyRadiusAdd,
            float bodyHeightScale,
            float bodyHeightAdd,
            int startSocketId,
            int endSocketId)
        {
            if (!Enum.IsDefined(typeof(CharacterActionHitVolumeSourceKind), sourceKind))
                throw new ArgumentOutOfRangeException(nameof(sourceKind), "Hit volume source kind is not defined.");
            if (!Enum.IsDefined(typeof(CharacterActionHitVolumeShapeKind), shapeKind))
                throw new ArgumentOutOfRangeException(nameof(shapeKind), "Hit volume shape kind is not defined.");
            if (bodyRadiusScale <= 0f)
                throw new ArgumentOutOfRangeException(nameof(bodyRadiusScale), "Body radius scale must be positive.");
            if (bodyHeightScale <= 0f)
                throw new ArgumentOutOfRangeException(nameof(bodyHeightScale), "Body height scale must be positive.");

            SourceKind = sourceKind;
            StartSocketId = startSocketId;
            EndSocketId = endSocketId;
            if (SourceKind == CharacterActionHitVolumeSourceKind.CasterBody)
            {
                ShapeKind = CharacterActionHitVolumeShapeKind.Capsule;
                LocalPointAX = 0f;
                LocalPointAY = 0f;
                LocalPointAZ = 0f;
                LocalPointBX = 0f;
                LocalPointBY = 0f;
                LocalPointBZ = 0f;
                Radius = 0f;
                BodyOffsetX = bodyOffsetX;
                BodyOffsetY = bodyOffsetY;
                BodyOffsetZ = bodyOffsetZ;
                BodyRadiusScale = bodyRadiusScale;
                BodyRadiusAdd = bodyRadiusAdd;
                BodyHeightScale = bodyHeightScale;
                BodyHeightAdd = bodyHeightAdd;
                StartSocketId = 0;
                EndSocketId = 0;
                return;
            }

            if (SourceKind == CharacterActionHitVolumeSourceKind.BakedSocketCapsule)
            {
                ShapeKind = radius > 0f ? CharacterActionHitVolumeShapeKind.Capsule : CharacterActionHitVolumeShapeKind.None;
                LocalPointAX = 0f;
                LocalPointAY = 0f;
                LocalPointAZ = 0f;
                LocalPointBX = 0f;
                LocalPointBY = 0f;
                LocalPointBZ = 0f;
                Radius = radius > 0f ? radius : 0f;
                BodyOffsetX = 0f;
                BodyOffsetY = 0f;
                BodyOffsetZ = 0f;
                BodyRadiusScale = 1f;
                BodyRadiusAdd = 0f;
                BodyHeightScale = 1f;
                BodyHeightAdd = 0f;
                return;
            }

            ShapeKind = radius > 0f ? shapeKind : CharacterActionHitVolumeShapeKind.None;
            LocalPointAX = localPointAX;
            LocalPointAY = localPointAY;
            LocalPointAZ = localPointAZ;
            LocalPointBX = localPointBX;
            LocalPointBY = localPointBY;
            LocalPointBZ = localPointBZ;
            Radius = radius > 0f ? radius : 0f;
            BodyOffsetX = 0f;
            BodyOffsetY = 0f;
            BodyOffsetZ = 0f;
            BodyRadiusScale = 1f;
            BodyRadiusAdd = 0f;
            BodyHeightScale = 1f;
            BodyHeightAdd = 0f;
            StartSocketId = 0;
            EndSocketId = 0;
        }

        public CharacterActionHitVolumeSourceKind SourceKind { get; }
        public CharacterActionHitVolumeShapeKind ShapeKind { get; }
        public float LocalPointAX { get; }
        public float LocalPointAY { get; }
        public float LocalPointAZ { get; }
        public float LocalPointBX { get; }
        public float LocalPointBY { get; }
        public float LocalPointBZ { get; }
        public float Radius { get; }
        public float BodyOffsetX { get; }
        public float BodyOffsetY { get; }
        public float BodyOffsetZ { get; }
        public float BodyRadiusScale { get; }
        public float BodyRadiusAdd { get; }
        public float BodyHeightScale { get; }
        public float BodyHeightAdd { get; }
        public int StartSocketId { get; }
        public int EndSocketId { get; }
        public bool HasVolume => SourceKind == CharacterActionHitVolumeSourceKind.CasterBody
            || (ShapeKind != CharacterActionHitVolumeShapeKind.None && Radius > 0f);

        public static CharacterActionHitVolumeConfig Capsule(
            float localPointAX,
            float localPointAY,
            float localPointAZ,
            float localPointBX,
            float localPointBY,
            float localPointBZ,
            float radius)
        {
            return new CharacterActionHitVolumeConfig(
                CharacterActionHitVolumeShapeKind.Capsule,
                localPointAX,
                localPointAY,
                localPointAZ,
                localPointBX,
                localPointBY,
                localPointBZ,
                radius);
        }

        public static CharacterActionHitVolumeConfig CasterBody()
        {
            return CasterBody(0f, 0f, 0f, 1f, 0f, 1f, 0f);
        }

        public static CharacterActionHitVolumeConfig CasterBody(
            float bodyOffsetX,
            float bodyOffsetY,
            float bodyOffsetZ,
            float bodyRadiusScale,
            float bodyRadiusAdd,
            float bodyHeightScale,
            float bodyHeightAdd)
        {
            return new CharacterActionHitVolumeConfig(
                CharacterActionHitVolumeSourceKind.CasterBody,
                CharacterActionHitVolumeShapeKind.Capsule,
                0f,
                0f,
                0f,
                0f,
                0f,
                0f,
                0f,
                bodyOffsetX,
                bodyOffsetY,
                bodyOffsetZ,
                bodyRadiusScale,
                bodyRadiusAdd,
                bodyHeightScale,
                bodyHeightAdd);
        }

        public static CharacterActionHitVolumeConfig BakedSocketCapsule(
            int startSocketId,
            int endSocketId,
            float radius)
        {
            if ((startSocketId <= 0))
                throw new ArgumentException("Start socket id is required.", nameof(startSocketId));
            if ((endSocketId <= 0))
                throw new ArgumentException("End socket id is required.", nameof(endSocketId));
            if (radius <= 0f)
                throw new ArgumentOutOfRangeException(nameof(radius), "Radius must be positive.");

            return new CharacterActionHitVolumeConfig(
                CharacterActionHitVolumeSourceKind.BakedSocketCapsule,
                CharacterActionHitVolumeShapeKind.Capsule,
                0f, 0f, 0f, 0f, 0f, 0f,
                radius,
                0f, 0f, 0f, 1f, 0f, 1f, 0f,
                startSocketId,
                endSocketId);
        }

        public bool Equals(CharacterActionHitVolumeConfig other)
        {
            return SourceKind == other.SourceKind
                && ShapeKind == other.ShapeKind
                && LocalPointAX.Equals(other.LocalPointAX)
                && LocalPointAY.Equals(other.LocalPointAY)
                && LocalPointAZ.Equals(other.LocalPointAZ)
                && LocalPointBX.Equals(other.LocalPointBX)
                && LocalPointBY.Equals(other.LocalPointBY)
                && LocalPointBZ.Equals(other.LocalPointBZ)
                && Radius.Equals(other.Radius)
                && BodyOffsetX.Equals(other.BodyOffsetX)
                && BodyOffsetY.Equals(other.BodyOffsetY)
                && BodyOffsetZ.Equals(other.BodyOffsetZ)
                && BodyRadiusScale.Equals(other.BodyRadiusScale)
                && BodyRadiusAdd.Equals(other.BodyRadiusAdd)
                && BodyHeightScale.Equals(other.BodyHeightScale)
                && BodyHeightAdd.Equals(other.BodyHeightAdd)
                && (StartSocketId == other.StartSocketId)
                && (EndSocketId == other.EndSocketId);
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterActionHitVolumeConfig other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)SourceKind;
                hash = (hash * 397) ^ (int)ShapeKind;
                hash = (hash * 397) ^ LocalPointAX.GetHashCode();
                hash = (hash * 397) ^ LocalPointAY.GetHashCode();
                hash = (hash * 397) ^ LocalPointAZ.GetHashCode();
                hash = (hash * 397) ^ LocalPointBX.GetHashCode();
                hash = (hash * 397) ^ LocalPointBY.GetHashCode();
                hash = (hash * 397) ^ LocalPointBZ.GetHashCode();
                hash = (hash * 397) ^ Radius.GetHashCode();
                hash = (hash * 397) ^ BodyOffsetX.GetHashCode();
                hash = (hash * 397) ^ BodyOffsetY.GetHashCode();
                hash = (hash * 397) ^ BodyOffsetZ.GetHashCode();
                hash = (hash * 397) ^ BodyRadiusScale.GetHashCode();
                hash = (hash * 397) ^ BodyRadiusAdd.GetHashCode();
                hash = (hash * 397) ^ BodyHeightScale.GetHashCode();
                hash = (hash * 397) ^ BodyHeightAdd.GetHashCode();
                hash = (hash * 397) ^ StartSocketId;
                hash = (hash * 397) ^ EndSocketId;
                return hash;
            }
        }
    }

    [Flags]
    public enum CharacterActionPartStructureStateMask
    {
        None = 0,
        Intact = 1 << 0,
        Broken = 1 << 1,
        Severed = 1 << 2,
        AllKnown = Intact | Broken | Severed
    }

    public readonly struct CombatTrackEvent
    {
        public CombatTrackEvent(
            int frame,
            CharacterActionTrackEventKind kind,
            int combatActionId = 0,
            int traceProfileId = 0,
            int stableEventId = 0,
            int equipmentSlotId = 0,
            CharacterActionDamagePayload damagePayload = default,
            CombatHitEventConfig hitEvent = default,
            CharacterActionHitVolumeConfig hitVolume = default,
            int endFrame = -1,
            int requiredPartStateId = 0,
            CharacterActionPartStructureStateMask allowedPartStructureStateMask =
                CharacterActionPartStructureStateMask.None,
            HitTargetStateFlags targetStateFlags = HitTargetStateFlags.None,
            string requiredPartStateLabel = "",
            int stableEventOrder = 0)
        {
            CharacterActionTrackEventValidation.ValidateFrameAndKind(frame, kind, CharacterActionTrackKind.Combat);
            if (kind == CharacterActionTrackEventKind.SetHitTargetState)
            {
                HitTargetStateContributionStore.ValidateFlags(targetStateFlags);
                if ((stableEventId <= 0))
                    throw new ArgumentException("SetHitTargetState requires a stable event id.", nameof(stableEventId));
                if (endFrame < frame)
                    throw new ArgumentOutOfRangeException(nameof(endFrame), "SetHitTargetState end frame must be greater than or equal to start frame.");
            }
            Frame = frame;
            Kind = kind;
            CombatActionId = combatActionId;
            TraceProfileId = traceProfileId;
            StableEventId = stableEventId;
            StableEventOrder = stableEventOrder;
            EquipmentSlotId = equipmentSlotId;
            DamagePayload = damagePayload;
            HitEvent = hitEvent.WithDefaults(StableEventId);
            HitVolume = hitVolume;
            EndFrame = endFrame < 0 ? frame : Math.Max(frame, endFrame);
            RequiredPartStateId = requiredPartStateId;
            RequiredPartStateLabel = requiredPartStateLabel ?? string.Empty;
            AllowedPartStructureStateMask = allowedPartStructureStateMask;
            TargetStateFlags = targetStateFlags;
        }

        public int Frame { get; }
        public CharacterActionTrackEventKind Kind { get; }
        public int CombatActionId { get; }
        public int TraceProfileId { get; }
        public int StableEventId { get; }
        public int StableEventOrder { get; }
        public int EquipmentSlotId { get; }
        public CharacterActionDamagePayload DamagePayload { get; }
        public CombatHitEventConfig HitEvent { get; }
        public CharacterActionHitVolumeConfig HitVolume { get; }
        public int EndFrame { get; }
        public int RequiredPartStateId { get; }
        public string RequiredPartStateLabel { get; }
        public CharacterActionPartStructureStateMask AllowedPartStructureStateMask { get; }
        public HitTargetStateFlags TargetStateFlags { get; }
    }

    public readonly struct GameplayTrackEvent
    {
        public GameplayTrackEvent(
            int frame,
            CharacterActionTrackEventKind kind,
            int requestId = 0,
            int abilityStableId = 0,
            int stableEventId = 0,
            int targetPolicyId = 0,
            int effectTimelineId = 0,
            CharacterActionDamagePayload damagePayload = default,
            CombatHitEventConfig hitEvent = default,
            int stableEventOrder = 0)
        {
            CharacterActionTrackEventValidation.ValidateFrameAndKind(frame, kind, CharacterActionTrackKind.Gameplay);
            Frame = frame;
            Kind = kind;
            RequestId = requestId;
            AbilityStableId = abilityStableId;
            StableEventId = stableEventId;
            StableEventOrder = stableEventOrder;
            TargetPolicyId = targetPolicyId;
            EffectTimelineId = effectTimelineId;
            DamagePayload = damagePayload;
            HitEvent = hitEvent.WithDefaults(StableEventId);
        }

        public int Frame { get; }
        public CharacterActionTrackEventKind Kind { get; }
        public int RequestId { get; }
        public int AbilityStableId { get; }
        public int StableEventId { get; }
        public int StableEventOrder { get; }
        public int TargetPolicyId { get; }
        public int EffectTimelineId { get; }
        public CharacterActionDamagePayload DamagePayload { get; }
        public CombatHitEventConfig HitEvent { get; }
    }

    public readonly struct AnimationTrackEvent
    {
        public AnimationTrackEvent(
            int frame,
            CharacterActionTrackEventKind kind,
            string animationActionKey = "",
            float transitionSeconds = 0f,
            int stableEventId = 0,
            int endFrame = -1,
            float playbackSpeed = 1f,
            string animationClipResourceKey = "",
            int animationStartOffsetFrames = 0,
            int animationEndOffsetFrames = 0,
            int stableEventOrder = 0)
        {
            CharacterActionTrackEventValidation.ValidateFrameAndKind(frame, kind, CharacterActionTrackKind.Animation);
            Frame = frame;
            Kind = kind;
            AnimationActionKey = animationActionKey ?? string.Empty;
            AnimationClipResourceKey = animationClipResourceKey ?? string.Empty;
            AnimationStartOffsetFrames = Math.Max(0, animationStartOffsetFrames);
            AnimationEndOffsetFrames = Math.Max(0, animationEndOffsetFrames);
            TransitionSeconds = transitionSeconds;
            StableEventId = stableEventId;
            StableEventOrder = stableEventOrder;
            PlaybackSpeed = playbackSpeed <= 0.0001f ? 1f : playbackSpeed;
            EndFrame = endFrame < 0 ? frame : Math.Max(frame, endFrame);
            var content = new RuntimeContentHashAccumulator();
            content.AddInt("frame", Frame); content.AddInt("endFrame", EndFrame); content.AddInt("kind", (int)Kind);
            content.AddStringStable("animation", AnimationActionKey); content.AddStringStable("clip", AnimationClipResourceKey);
            content.AddInt("startOffset", AnimationStartOffsetFrames); content.AddInt("endOffset", AnimationEndOffsetFrames);
            content.AddInt("speed", BitConverter.SingleToInt32Bits(PlaybackSpeed));
            ContentWitnessHash = content.ToHash();
        }

        public long ContentWitnessHash { get; }
        public int Frame { get; }
        public int EndFrame { get; }
        public float PlaybackSpeed { get; }
        public CharacterActionTrackEventKind Kind { get; }
        public string AnimationActionKey { get; }
        public string AnimationClipResourceKey { get; }
        public string EffectiveAnimationKey => CharacterActionAnimationKeyUtility.ResolveEffectiveKey(
            AnimationClipResourceKey,
            AnimationActionKey);
        public int AnimationStartOffsetFrames { get; }
        public int AnimationEndOffsetFrames { get; }
        public float TransitionSeconds { get; }
        public int StableEventId { get; }
        public int StableEventOrder { get; }
    }

    public static class CharacterActionAnimationKeyUtility
    {
        public const int TimelineFramesPerSecond = 60;

        public static string ResolveEffectiveKey(string animationClipResourceKey, string animationActionKey)
        {
            return string.IsNullOrWhiteSpace(animationClipResourceKey)
                ? (animationActionKey ?? string.Empty)
                : animationClipResourceKey;
        }

        public static float FramesToSeconds(int frames)
        {
            return Math.Max(0, frames) / (float)TimelineFramesPerSecond;
        }
    }

    public enum CharacterActionVfxStopPolicy
    {
        Complete = 0,
        Dissipate = 1,
        Immediate = 2,
    }

    /// <summary>Defines whether a visual cue uses its resource duration or the authored event window.</summary>
    public enum CharacterActionVfxPlaybackMode
    {
        PlayToCompletion = 0,
        StopAtEventEnd = 1,
        LoopUntilEventEnd = 2,
    }

    /// <summary>Combines the shared attachment transform with VFX-specific playback, exit, and parameter controls.</summary>
    /// <remarks>NoAlloc value contract.</remarks>
    public readonly struct CharacterActionVfxEventConfig : IEquatable<CharacterActionVfxEventConfig>
    {
        public const float MinPlaybackRate = 0.05f;
        public const float MaxPlaybackRate = 16f;
        public const float MinScaleMultiplier = 0.01f;
        public const float MaxScaleMultiplier = 100f;

        public static readonly CharacterActionVfxEventConfig Default = new CharacterActionVfxEventConfig();

        public CharacterActionVfxEventConfig(
            MxAttachmentTransform attachment = default,
            CharacterActionVfxPlaybackMode playbackMode = CharacterActionVfxPlaybackMode.PlayToCompletion,
            float playbackRate = 1f,
            float scaleMultiplier = 1f,
            int startOffsetFrames = 0,
            int endOffsetFrames = 0,
            CharacterActionVfxStopPolicy stopPolicy = CharacterActionVfxStopPolicy.Complete,
            int parameter0 = 0,
            int parameter1 = 0,
            int parameter2 = 0,
            int parameter3 = 0,
            int vfxDefinitionId = 0,
            int numericSocketId = 0,
            bool useWeaponBladeTrailStyle = false)
        {
            if (!Enum.IsDefined(typeof(CharacterActionVfxPlaybackMode), playbackMode))
                throw new ArgumentOutOfRangeException(nameof(playbackMode));
            if (!Enum.IsDefined(typeof(CharacterActionVfxStopPolicy), stopPolicy))
                throw new ArgumentOutOfRangeException(nameof(stopPolicy));

            if (vfxDefinitionId < 0 || numericSocketId < 0) throw new ArgumentOutOfRangeException(nameof(vfxDefinitionId));
            VfxDefinitionId = vfxDefinitionId;
            NumericSocketId = numericSocketId;
            UseWeaponBladeTrailStyle = useWeaponBladeTrailStyle;
            Attachment = MxAttachmentTransform.Normalize(attachment);
            PlaybackMode = playbackMode;
            PlaybackRate = ClampFinite(playbackRate, MinPlaybackRate, MaxPlaybackRate, 1f);
            ScaleMultiplier = ClampFinite(scaleMultiplier, MinScaleMultiplier, MaxScaleMultiplier, 1f);
            StartOffsetFrames = Math.Max(0, startOffsetFrames);
            EndOffsetFrames = Math.Max(0, endOffsetFrames);
            StopPolicy = stopPolicy;
            Parameter0 = parameter0;
            Parameter1 = parameter1;
            Parameter2 = parameter2;
            Parameter3 = parameter3;
        }

        public int VfxDefinitionId { get; }
        public int NumericSocketId { get; }
        public bool UseWeaponBladeTrailStyle { get; }
        public MxAttachmentTransform Attachment { get; }
        /// <summary>Gets the event-to-resource playback window contract.</summary>
        public CharacterActionVfxPlaybackMode PlaybackMode { get; }
        /// <summary>Gets the instance-local simulation multiplier.</summary>
        public float PlaybackRate { get; }
        /// <summary>Gets the uniform multiplier applied after the shared attachment transform scale.</summary>
        public float ScaleMultiplier { get; }
        /// <summary>Gets the number of 60 Hz source frames skipped before playback begins.</summary>
        public int StartOffsetFrames { get; }
        /// <summary>Gets the number of 60 Hz source frames removed from the natural tail.</summary>
        public int EndOffsetFrames { get; }
        public CharacterActionVfxStopPolicy StopPolicy { get; }
        public int Parameter0 { get; }
        public int Parameter1 { get; }
        public int Parameter2 { get; }
        public int Parameter3 { get; }

        public bool Equals(CharacterActionVfxEventConfig other)
        {
            return VfxDefinitionId == other.VfxDefinitionId
                && NumericSocketId == other.NumericSocketId
                && UseWeaponBladeTrailStyle == other.UseWeaponBladeTrailStyle
                && Attachment.Equals(other.Attachment)
                && PlaybackMode == other.PlaybackMode
                && PlaybackRate.Equals(other.PlaybackRate)
                && ScaleMultiplier.Equals(other.ScaleMultiplier)
                && StartOffsetFrames == other.StartOffsetFrames
                && EndOffsetFrames == other.EndOffsetFrames
                && StopPolicy == other.StopPolicy
                && Parameter0 == other.Parameter0
                && Parameter1 == other.Parameter1
                && Parameter2 == other.Parameter2
                && Parameter3 == other.Parameter3;
        }

        public override bool Equals(object obj) => obj is CharacterActionVfxEventConfig other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Attachment.GetHashCode();
                hash = (hash * 397) ^ VfxDefinitionId;
                hash = (hash * 397) ^ UseWeaponBladeTrailStyle.GetHashCode();
                hash = (hash * 397) ^ NumericSocketId;
                hash = (hash * 397) ^ (int)PlaybackMode;
                hash = (hash * 397) ^ PlaybackRate.GetHashCode();
                hash = (hash * 397) ^ ScaleMultiplier.GetHashCode();
                hash = (hash * 397) ^ StartOffsetFrames;
                hash = (hash * 397) ^ EndOffsetFrames;
                hash = (hash * 397) ^ (int)StopPolicy;
                hash = (hash * 397) ^ Parameter0;
                hash = (hash * 397) ^ Parameter1;
                hash = (hash * 397) ^ Parameter2;
                hash = (hash * 397) ^ Parameter3;
                return hash;
            }
        }

        private static float ClampFinite(float value, float min, float max, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return fallback;
            return Math.Max(min, Math.Min(max, value));
        }
    }

    public readonly struct PresentationTrackEvent
    {
        public PresentationTrackEvent(
            int frame,
            CharacterActionTrackEventKind kind,
            int cueId = 0,
            string resourceKey = "",
            int stableEventId = 0,
            int endFrame = -1,
            CharacterActionVfxEventConfig vfxConfig = default,
            int stableEventOrder = 0)
        {
            CharacterActionTrackEventValidation.ValidateFrameAndKind(frame, kind, CharacterActionTrackKind.Presentation);
            Frame = frame;
            EndFrame = endFrame < 0 ? frame : Math.Max(frame, endFrame);
            Kind = kind;
            CueId = cueId;
            ResourceKey = resourceKey ?? string.Empty;
            StableEventId = stableEventId;
            StableEventOrder = stableEventOrder;
            VfxConfig = !vfxConfig.Attachment.Transform.IsScaleValid()
                ? new CharacterActionVfxEventConfig()
                : vfxConfig;
        }

        public int Frame { get; }
        public int EndFrame { get; }
        public CharacterActionTrackEventKind Kind { get; }
        public int CueId { get; }
        public string ResourceKey { get; }
        public int StableEventId { get; }
        public int StableEventOrder { get; }
        public CharacterActionVfxEventConfig VfxConfig { get; }
    }

    public readonly struct DebugTrackEvent
    {
        public DebugTrackEvent(int frame, CharacterActionTrackEventKind kind, string markerId = "", int stableEventId = 0,
            int stableEventOrder = 0)
        {
            CharacterActionTrackEventValidation.ValidateFrameAndKind(frame, kind, CharacterActionTrackKind.Debug);
            Frame = frame;
            Kind = kind;
            MarkerId = markerId ?? string.Empty;
            StableEventId = stableEventId;
            StableEventOrder = stableEventOrder;
        }

        public int Frame { get; }
        public CharacterActionTrackEventKind Kind { get; }
        public string MarkerId { get; }
        public int StableEventId { get; }
        public int StableEventOrder { get; }
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

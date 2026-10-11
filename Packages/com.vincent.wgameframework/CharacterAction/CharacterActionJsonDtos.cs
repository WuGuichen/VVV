using System;
using MxFramework.Core;
using Newtonsoft.Json;

namespace MxFramework.CharacterAction
{
    public sealed class CharacterActionSetsJsonDocument
    {
        public string SchemaVersion { get; set; } = string.Empty;
        public CharacterActionSetJsonDto[] ActionSets { get; set; } = Array.Empty<CharacterActionSetJsonDto>();
    }

    public sealed class CharacterActionsJsonDocument
    {
        public string SchemaVersion { get; set; } = string.Empty;
        public CharacterActionJsonDto[] Actions { get; set; } = Array.Empty<CharacterActionJsonDto>();
    }

    public sealed class CharacterActionProfilesJsonDocument
    {
        public string SchemaVersion { get; set; } = string.Empty;
        public CharacterActionProfileJsonDto[] Profiles { get; set; } = Array.Empty<CharacterActionProfileJsonDto>();
    }

    public sealed class CharacterReactionProfilesJsonDocument
    {
        public string SchemaVersion { get; set; } = string.Empty;
        public CharacterReactionProfileJsonDto[] ReactionProfiles { get; set; } = Array.Empty<CharacterReactionProfileJsonDto>();
    }

    public sealed class CharacterLandingProfilesJsonDocument
    {
        public string SchemaVersion { get; set; } = string.Empty;
        public CharacterLandingProfileJsonDto[] LandingProfiles { get; set; } =
            Array.Empty<CharacterLandingProfileJsonDto>();
    }

    public sealed class CharacterLandingProfileJsonDto
    {
        public string StableId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int LightLandingActionId { get; set; }
        public int HeavyLandingActionId { get; set; }
        public int LightLandingMinDownwardSpeedMillimetersPerSecond { get; set; } =
            CharacterLandingProfile.DefaultLightLandingMinDownwardSpeedMillimetersPerSecond;
        public int HeavyLandingMinDownwardSpeedMillimetersPerSecond { get; set; } =
            CharacterLandingProfile.DefaultHeavyLandingMinDownwardSpeedMillimetersPerSecond;
    }

    public sealed class CharacterActionProfileJsonDto
    {
        public int Id { get; set; }
        public string StableId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string[] ActionSetIds { get; set; } = Array.Empty<string>();
        public string[] ActionLibraryIds { get; set; } = Array.Empty<string>();
    }

    public sealed class CharacterReactionProfileJsonDto
    {
        public string StableId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int DefaultActionId { get; set; }
        public CharacterReactionRuleJsonDto[] Rules { get; set; } = Array.Empty<CharacterReactionRuleJsonDto>();
    }

    public sealed class CharacterReactionRuleJsonDto
    {
        public int ActionId { get; set; }
        public string Trigger { get; set; } = string.Empty;
        public bool RequiresBodyPart { get; set; }
        public bool RequiresHitZone { get; set; }
        public bool RequiresHitDirection { get; set; }
        public bool RequiresDamageType { get; set; }
        public bool RequiresReactionGroup { get; set; }
        public string CurrentScatterPressureBand { get; set; } = string.Empty;
        public bool? IsDeath { get; set; }
        public bool? IsAirborne { get; set; }
        public string CurrentPhase { get; set; } = string.Empty;
        public bool? CurrentActionCommitted { get; set; }
        public bool? CurrentActionInterruptible { get; set; }
        public int Priority { get; set; }
        public int BodyPartId { get; set; }
        public int HitZoneId { get; set; }
        public int DamageTypeId { get; set; }
        public string HitDirection { get; set; } = string.Empty;
        public int? MinImpactForce { get; set; }
        public int? MaxImpactForce { get; set; }
        public int ReactionGroupId { get; set; }
        public bool RequiresImpactForce { get; set; }
    }

    public sealed class CharacterActionSetJsonDto
    {
        public int Id { get; set; }
        public string StableId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string CharacterStableId { get; set; } = string.Empty;
        public string EquipmentStateStableId { get; set; } = string.Empty;
        public int MovementProfileId { get; set; }
        public string ReactionProfileId { get; set; } = string.Empty;
        public string LandingProfileId { get; set; } = string.Empty;
        public int DefaultActionId { get; set; }
        public int DefaultActionSlotMask { get; set; }
        public CharacterActionBindingJsonDto[] CommandBindings { get; set; } = Array.Empty<CharacterActionBindingJsonDto>();
        public CharacterActionCommandSequenceJsonDto[] CommandSequences { get; set; } = Array.Empty<CharacterActionCommandSequenceJsonDto>();
        public CharacterAbilityActionBindingJsonDto[] AbilityBindings { get; set; } = Array.Empty<CharacterAbilityActionBindingJsonDto>();
        public CharacterReactionBindingJsonDto[] ReactionBindings { get; set; } = Array.Empty<CharacterReactionBindingJsonDto>();
    }

    public sealed class CharacterActionJsonDto
    {
        public int ActionId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string TimelineAuthority { get; set; } = string.Empty;
        public string[] Tags { get; set; } = Array.Empty<string>();
        public int Priority { get; set; }
        public int? DurationFrames { get; set; }
        public CharacterActionRequirementJsonDto[] Requirements { get; set; } = Array.Empty<CharacterActionRequirementJsonDto>();
        public CharacterActionPhaseJsonDto[] Phases { get; set; } = Array.Empty<CharacterActionPhaseJsonDto>();
        public CharacterCancelRuleJsonDto[] CancelRules { get; set; } = Array.Empty<CharacterCancelRuleJsonDto>();
        public CharacterInterruptRuleJsonDto[] InterruptRules { get; set; } = Array.Empty<CharacterInterruptRuleJsonDto>();
        public CharacterActionCohesionSupportJsonDto CohesionSupport { get; set; }
        public MotionTrackJsonDto MotionTrack { get; set; }
        public CombatTrackJsonDto CombatTrack { get; set; }
        public GameplayTrackJsonDto GameplayTrack { get; set; }
        public AnimationTrackJsonDto AnimationTrack { get; set; }
        public PresentationTrackJsonDto PresentationTrack { get; set; }
        public DebugTrackJsonDto DebugTrack { get; set; }
    }

    public sealed class CharacterActionBindingJsonDto
    {
        public int IntentId { get; set; }
        public int ActionId { get; set; }
        public int Priority { get; set; }
        public bool AllowQueue { get; set; }
        public int QueueWindowFrames { get; set; }
    }

    public sealed class CharacterActionCommandSequenceJsonDto
    {
        public int IntentId { get; set; }
        public int[] ActionIds { get; set; } = Array.Empty<int>();
        public bool WrapAfterLast { get; set; }
        public int Priority { get; set; }
        public bool AllowQueue { get; set; }
        public int QueueWindowFrames { get; set; }
    }

    public sealed class CharacterAbilityActionBindingJsonDto
    {
        public int AbilityId { get; set; }
        public int ActionId { get; set; }
        public string[] RequiredTags { get; set; } = Array.Empty<string>();
        public string[] ForbiddenTags { get; set; } = Array.Empty<string>();
    }

    public sealed class CharacterReactionBindingJsonDto
    {
        public string ReactionProfileId { get; set; } = string.Empty;
        public int DefaultActionId { get; set; }
        public string[] RequiredTags { get; set; } = Array.Empty<string>();
        public string[] ForbiddenTags { get; set; } = Array.Empty<string>();
    }

    public sealed class CharacterActionRequirementJsonDto
    {
        public string Kind { get; set; } = string.Empty;
        public string StableId { get; set; } = string.Empty;
        public int Value { get; set; }
        public int ReferenceId { get; set; }
    }

    public sealed class CharacterActionPhaseJsonDto
    {
        public string Kind { get; set; } = string.Empty;
        public int StartFrame { get; set; }
        public int EndFrame { get; set; }
        public string CombatPhaseAnchor { get; set; } = string.Empty;
        public bool RequiresCombatPhaseMatch { get; set; } = true;

        public bool ShouldSerializeCombatPhaseAnchor()
        {
            return !string.IsNullOrWhiteSpace(CombatPhaseAnchor)
                && !string.Equals(CombatPhaseAnchor, CharacterActionPhaseKind.None.ToString(), StringComparison.Ordinal);
        }

        public bool ShouldSerializeRequiresCombatPhaseMatch()
        {
            return ShouldSerializeCombatPhaseAnchor() || !RequiresCombatPhaseMatch;
        }
    }

    public sealed class CharacterCancelRuleJsonDto
    {
        public int StartFrame { get; set; }
        public int EndFrame { get; set; }
        public int RequestTypeMask { get; set; }
        public int ActionSlotMask { get; set; }
    }

    public sealed class CharacterInterruptRuleJsonDto
    {
        public string SourceKind { get; set; } = string.Empty;
        public int MinimumPriority { get; set; }
        public int TargetActionId { get; set; }
        public bool Allow { get; set; } = true;
    }

    public sealed class CharacterActionCohesionSupportJsonDto
    {
        public bool Enabled { get; set; }
        public float SupportRate { get; set; } = 1f;
        public float MinSupportRatio { get; set; } = 1f;
        public bool HasCohesionSupportWindow { get; set; }
        public int CohesionSupportWindowStartFrame { get; set; }
        public int CohesionSupportWindowEndFrame { get; set; }
    }

    public sealed class MotionTrackJsonDto
    {
        public bool UsesRootMotion { get; set; }
        public RootMotionTrackJsonDto RootMotion { get; set; }
        public VerticalRootMotionTrackJsonDto VerticalRootMotion { get; set; }
        public MotionTrackEventJsonDto[] Events { get; set; } = Array.Empty<MotionTrackEventJsonDto>();

        public bool ShouldSerializeVerticalRootMotion()
        {
            return VerticalRootMotion != null;
        }
    }

    public sealed class RootMotionTrackJsonDto
    {
        public int SourceAnimationEventId { get; set; }
        public string SourceAnimationClipResourceKey { get; set; } = string.Empty;
        public string Space { get; set; } = CharacterActionRootMotionSpace.Local.ToString();
        public int SampleTickRate { get; set; } = CharacterActionAnimationKeyUtility.TimelineFramesPerSecond;
        public string ObstaclePolicy { get; set; } = CharacterActionRootMotionObstaclePolicy.Default.ToString();
        public RootMotionFrameJsonDto[] Frames { get; set; } = Array.Empty<RootMotionFrameJsonDto>();
    }

    public sealed class RootMotionFrameJsonDto
    {
        public int Frame { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
    }

    public sealed class VerticalRootMotionTrackJsonDto
    {
        public int SourceAnimationEventId { get; set; }
        public string SourceAnimationClipResourceKey { get; set; } = string.Empty;
        public VerticalRootMotionFrameJsonDto[] Frames { get; set; } =
            Array.Empty<VerticalRootMotionFrameJsonDto>();
    }

    public sealed class VerticalRootMotionFrameJsonDto
    {
        public int Frame { get; set; }
        public float Y { get; set; }
    }

    public sealed class CombatTrackJsonDto
    {
        public int CombatActionId { get; set; }
        public CombatTrackEventJsonDto[] Events { get; set; } = Array.Empty<CombatTrackEventJsonDto>();
    }

    public sealed class GameplayTrackJsonDto
    {
        public GameplayTrackEventJsonDto[] Events { get; set; } = Array.Empty<GameplayTrackEventJsonDto>();
    }

    public sealed class AnimationTrackJsonDto
    {
        public AnimationTrackEventJsonDto[] Events { get; set; } = Array.Empty<AnimationTrackEventJsonDto>();
    }

    public sealed class PresentationTrackJsonDto
    {
        public PresentationTrackEventJsonDto[] Events { get; set; } = Array.Empty<PresentationTrackEventJsonDto>();
    }

    public sealed class DebugTrackJsonDto
    {
        public DebugTrackEventJsonDto[] Events { get; set; } = Array.Empty<DebugTrackEventJsonDto>();
    }

    public sealed class MotionTrackEventJsonDto
    {
        public int Frame { get; set; }
        public int EndFrame { get; set; }
        public string Kind { get; set; } = string.Empty;
        public string MovementMode { get; set; } = string.Empty;
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public string Space { get; set; } = CharacterActionMotionVectorSpace.World.ToString();
        public float? MovementControlScale { get; set; }
        public float? RotationControlScale { get; set; }
        public int CollisionProfileId { get; set; }
        public string CollisionRestorePolicy { get; set; } =
            CharacterActionCollisionProfileRestorePolicy.OnActionEnd.ToString();
        public int CollisionTransitionFrames { get; set; }
        public int StableEventId { get; set; }
        public int StableEventOrder { get; set; }

        public bool ShouldSerializeEndFrame()
        {
            return string.Equals(Kind, CharacterActionTrackEventKind.LockMovement.ToString(), StringComparison.Ordinal)
                || string.Equals(Kind, CharacterActionTrackEventKind.SetControlScale.ToString(), StringComparison.Ordinal)
                || string.Equals(Kind, CharacterActionTrackEventKind.SetCollisionProfile.ToString(), StringComparison.Ordinal)
                || string.Equals(Kind, CharacterActionTrackEventKind.SetVerticalVelocity.ToString(), StringComparison.Ordinal)
                || string.Equals(Kind, CharacterActionTrackEventKind.IgnoreGravity.ToString(), StringComparison.Ordinal)
                || string.Equals(Kind, CharacterActionTrackEventKind.SampleRootMotionY.ToString(), StringComparison.Ordinal);
        }

        public bool ShouldSerializeMovementMode()
        {
            return string.Equals(Kind, CharacterActionTrackEventKind.SetMovementMode.ToString(), StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(MovementMode);
        }

        public bool ShouldSerializeSpace()
        {
            return string.Equals(Kind, CharacterActionTrackEventKind.ApplyImpulse.ToString(), StringComparison.Ordinal)
                && !string.Equals(Space, CharacterActionMotionVectorSpace.World.ToString(), StringComparison.Ordinal);
        }

        public bool ShouldSerializeMovementControlScale()
        {
            return string.Equals(Kind, CharacterActionTrackEventKind.SetControlScale.ToString(), StringComparison.Ordinal)
                && MovementControlScale.HasValue;
        }

        public bool ShouldSerializeRotationControlScale()
        {
            return string.Equals(Kind, CharacterActionTrackEventKind.SetControlScale.ToString(), StringComparison.Ordinal)
                && RotationControlScale.HasValue;
        }

        public bool ShouldSerializeCollisionProfileId()
        {
            return string.Equals(Kind, CharacterActionTrackEventKind.SetCollisionProfile.ToString(), StringComparison.Ordinal)
                && (CollisionProfileId > 0);
        }

        public bool ShouldSerializeCollisionRestorePolicy()
        {
            return string.Equals(Kind, CharacterActionTrackEventKind.SetCollisionProfile.ToString(), StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(CollisionRestorePolicy)
                && !string.Equals(
                    CollisionRestorePolicy,
                    CharacterActionCollisionProfileRestorePolicy.OnActionEnd.ToString(),
                    StringComparison.Ordinal);
        }

        public bool ShouldSerializeCollisionTransitionFrames()
        {
            return string.Equals(Kind, CharacterActionTrackEventKind.SetCollisionProfile.ToString(), StringComparison.Ordinal)
                && CollisionTransitionFrames > 0;
        }
    }

    public sealed class CharacterActionBladeTrailJsonDto
    {
        public bool Enabled { get; set; }
        public int AdvanceFrames { get; set; }
        public int DelayFrames { get; set; }
        public int StableEventId { get; set; }
    }

    public sealed class CombatTrackEventJsonDto
    {
        public int Frame { get; set; }
        public int EndFrame { get; set; }
        public string Kind { get; set; } = string.Empty;
        public int CombatActionId { get; set; }
        public int TraceProfileId { get; set; }
        public int EquipmentSlotId { get; set; }
        public int StableEventId { get; set; }
        public int StableEventOrder { get; set; }
        public int RequiredPartStateId { get; set; }
        public string RequiredPartStateLabel { get; set; } = string.Empty;
        public int AllowedPartStructureStateMask { get; set; }
        public int TargetStateMask { get; set; }
        public CharacterActionHitEventJsonDto HitEvent { get; set; }
        public CharacterActionHitVolumeJsonDto HitVolume { get; set; }
        public CharacterActionBladeTrailJsonDto BladeTrail { get; set; }
        public bool ShouldSerializeBladeTrail() => BladeTrail != null &&
            string.Equals(Kind, CharacterActionTrackEventKind.HitTraceWindow.ToString(), StringComparison.Ordinal);

        public bool ShouldSerializeEndFrame()
        {
            return string.Equals(Kind, CharacterActionTrackEventKind.HitTraceWindow.ToString(), StringComparison.Ordinal)
                || string.Equals(Kind, CharacterActionTrackEventKind.SetHitTargetState.ToString(), StringComparison.Ordinal);
        }

        public bool ShouldSerializeTargetStateMask()
        {
            return string.Equals(Kind, CharacterActionTrackEventKind.SetHitTargetState.ToString(), StringComparison.Ordinal);
        }

        public bool ShouldSerializeEquipmentSlotId()
        {
            return (EquipmentSlotId > 0);
        }

        public bool ShouldSerializeHitEvent()
        {
            return HitEvent != null && HitEvent.ShouldSerialize();
        }

        public bool ShouldSerializeHitVolume()
        {
            return HitVolume != null && HitVolume.ShouldSerialize();
        }

        public bool ShouldSerializeRequiredPartStateId()
        {
            return RequiredPartStateId > 0;
        }

        public bool ShouldSerializeAllowedPartStructureStateMask()
        {
            return AllowedPartStructureStateMask != 0;
        }

    }

    public sealed class CharacterActionHitVolumeJsonDto
    {
        public string SourceKind { get; set; } = string.Empty;
        public string ShapeKind { get; set; } = string.Empty;
        public CharacterActionHitVolumePointJsonDto LocalPointA { get; set; }
        public CharacterActionHitVolumePointJsonDto LocalPointB { get; set; }
        public float Radius { get; set; }
        public CharacterActionHitVolumePointJsonDto BodyOffset { get; set; }
        public float BodyRadiusScale { get; set; } = 1f;
        public float BodyRadiusAdd { get; set; }
        public float BodyHeightScale { get; set; } = 1f;
        public float BodyHeightAdd { get; set; }
        public int StartSocketId { get; set; }
        public int EndSocketId { get; set; }

        public bool ShouldSerializeSourceKind()
        {
            return !string.IsNullOrWhiteSpace(SourceKind)
                && !string.Equals(
                    SourceKind,
                    CharacterActionHitVolumeSourceKind.ManualCapsule.ToString(),
                    StringComparison.Ordinal);
        }

        public bool ShouldSerializeShapeKind()
        {
            return !IsCasterBody() && !IsBakedSocketCapsule()
                && !string.IsNullOrWhiteSpace(ShapeKind);
        }

        public bool ShouldSerializeLocalPointA()
        {
            return !IsCasterBody() && !IsBakedSocketCapsule()
                && LocalPointA != null
                && LocalPointA.ShouldSerialize();
        }

        public bool ShouldSerializeLocalPointB()
        {
            return !IsCasterBody() && !IsBakedSocketCapsule()
                && LocalPointB != null
                && LocalPointB.ShouldSerialize();
        }

        public bool ShouldSerializeRadius()
        {
            return !IsCasterBody() && Radius > 0f;
        }

        public bool ShouldSerializeBodyOffset()
        {
            return IsCasterBody()
                && BodyOffset != null
                && BodyOffset.ShouldSerialize();
        }

        public bool ShouldSerializeBodyRadiusScale()
        {
            return IsCasterBody() && !BodyRadiusScale.Equals(1f);
        }

        public bool ShouldSerializeBodyRadiusAdd()
        {
            return IsCasterBody() && !BodyRadiusAdd.Equals(0f);
        }

        public bool ShouldSerializeBodyHeightScale()
        {
            return IsCasterBody() && !BodyHeightScale.Equals(1f);
        }

        public bool ShouldSerializeBodyHeightAdd()
        {
            return IsCasterBody() && !BodyHeightAdd.Equals(0f);
        }

        public bool ShouldSerializeStartSocketId()
        {
            return IsBakedSocketCapsule() && (StartSocketId > 0);
        }

        public bool ShouldSerializeEndSocketId()
        {
            return IsBakedSocketCapsule() && (EndSocketId > 0);
        }

        internal bool ShouldSerialize()
        {
            return ShouldSerializeSourceKind()
                || ShouldSerializeShapeKind()
                || ShouldSerializeLocalPointA()
                || ShouldSerializeLocalPointB()
                || ShouldSerializeRadius()
                || ShouldSerializeBodyOffset()
                || ShouldSerializeBodyRadiusScale()
                || ShouldSerializeBodyRadiusAdd()
                || ShouldSerializeBodyHeightScale()
                || ShouldSerializeBodyHeightAdd()
                || ShouldSerializeStartSocketId()
                || ShouldSerializeEndSocketId();
        }

        private bool IsCasterBody()
        {
            return string.Equals(
                SourceKind,
                CharacterActionHitVolumeSourceKind.CasterBody.ToString(),
                StringComparison.Ordinal);
        }

        private bool IsBakedSocketCapsule()
        {
            return string.Equals(
                SourceKind,
                CharacterActionHitVolumeSourceKind.BakedSocketCapsule.ToString(),
                StringComparison.Ordinal);
        }
    }

    public sealed class CharacterActionHitVolumePointJsonDto
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }

        public bool ShouldSerializeX()
        {
            return !X.Equals(0f);
        }

        public bool ShouldSerializeY()
        {
            return !Y.Equals(0f);
        }

        public bool ShouldSerializeZ()
        {
            return !Z.Equals(0f);
        }

        internal bool ShouldSerialize()
        {
            return ShouldSerializeX() || ShouldSerializeY() || ShouldSerializeZ();
        }
    }

    public sealed class CharacterActionHitEventJsonDto
    {
        public string[] TargetTypes { get; set; } = Array.Empty<string>();
        public string EnvironmentHitPolicy { get; set; } = string.Empty;
        public int StableEventId { get; set; }
        public int StableEventOrder { get; set; }
        public int MaxAcceptedHits { get; set; }
        public string HitSameTargetPolicy { get; set; } = string.Empty;
        public CharacterActionHitDamageJsonDto Damage { get; set; }
        public CharacterActionHitBuffJsonDto[] OnHitBuffs { get; set; } = Array.Empty<CharacterActionHitBuffJsonDto>();

        public bool ShouldSerializeTargetTypes()
        {
            return TargetTypes != null && TargetTypes.Length > 0;
        }

        public bool ShouldSerializeEnvironmentHitPolicy()
        {
            return !string.IsNullOrWhiteSpace(EnvironmentHitPolicy);
        }

        public bool ShouldSerializeStableEventId()
        {
            return (StableEventId > 0);
        }

        public bool ShouldSerializeMaxAcceptedHits()
        {
            return MaxAcceptedHits > 0 && MaxAcceptedHits != 1;
        }

        public bool ShouldSerializeHitSameTargetPolicy()
        {
            return !string.IsNullOrWhiteSpace(HitSameTargetPolicy);
        }

        public bool ShouldSerializeDamage()
        {
            return Damage != null && Damage.ShouldSerialize();
        }

        public bool ShouldSerializeOnHitBuffs()
        {
            return OnHitBuffs != null && OnHitBuffs.Length > 0;
        }

        internal bool ShouldSerialize()
        {
            return ShouldSerializeTargetTypes()
                || ShouldSerializeEnvironmentHitPolicy()
                || ShouldSerializeStableEventId()
                || ShouldSerializeMaxAcceptedHits()
                || ShouldSerializeHitSameTargetPolicy()
                || ShouldSerializeDamage()
                || ShouldSerializeOnHitBuffs();
        }
    }

    public sealed class CharacterActionHitBuffJsonDto
    {
        public int BuffId { get; set; }
    }

    public sealed class CharacterActionHitDamageJsonDto
    {
        public int DamageTypeId { get; set; }
        public int FormulaId { get; set; }
        public string DamageTypeLabel { get; set; } = string.Empty;
        public string FormulaLabel { get; set; } = string.Empty;
        public int BaseDamage { get; set; }
        public CharacterActionDamageAttributeScaleJsonDto[] SourceAttributeScales { get; set; } = Array.Empty<CharacterActionDamageAttributeScaleJsonDto>();
        public int ImpactPower { get; set; }
        public int LaunchPower { get; set; }
        public int[] Tags { get; set; } = Array.Empty<int>();
        public string[] TagLabels { get; set; } = Array.Empty<string>();
        public int AnalysisCategory { get; set; }
        public string AnalysisCategoryLabel { get; set; } = string.Empty;

        public bool ShouldSerializeDamageTypeId()
        {
            return (DamageTypeId > 0);
        }

        public bool ShouldSerializeFormulaId()
        {
            return false; // Legacy input is read for migration; CharacterTest chooses its formula globally.
        }

        public bool ShouldSerializeFormulaLabel() => false;

        public bool ShouldSerializeBaseDamage()
        {
            return BaseDamage > 0;
        }

        public bool ShouldSerializeSourceAttributeScales()
        {
            return SourceAttributeScales != null && SourceAttributeScales.Length > 0;
        }

        public bool ShouldSerializeImpactPower()
        {
            return ImpactPower > 0;
        }

        public bool ShouldSerializeLaunchPower()
        {
            return LaunchPower > 0;
        }

        public bool ShouldSerializeTags()
        {
            return Tags != null && Tags.Length > 0;
        }

        public bool ShouldSerializeAnalysisCategory()
        {
            return AnalysisCategory > 0;
        }

        internal bool ShouldSerialize()
        {
            return ShouldSerializeDamageTypeId()
                || ShouldSerializeFormulaId()
                || ShouldSerializeBaseDamage()
                || ShouldSerializeSourceAttributeScales()
                || ShouldSerializeImpactPower()
                || ShouldSerializeLaunchPower()
                || ShouldSerializeTags()
                || ShouldSerializeAnalysisCategory();
        }
    }

    public sealed class GameplayTrackEventJsonDto
    {
        public int Frame { get; set; }
        public string Kind { get; set; } = string.Empty;
        public int RequestId { get; set; }
        public int AbilityStableId { get; set; }
        public int TargetPolicyId { get; set; }
        public int EffectTimelineId { get; set; }
        public int StableEventId { get; set; }
        public int StableEventOrder { get; set; }
        public CharacterActionHitEventJsonDto HitEvent { get; set; }

        public bool ShouldSerializeTargetPolicyId()
        {
            return (TargetPolicyId > 0);
        }

        public bool ShouldSerializeEffectTimelineId()
        {
            return (EffectTimelineId > 0);
        }

        public bool ShouldSerializeHitEvent()
        {
            return HitEvent != null && HitEvent.ShouldSerialize();
        }

    }

    public sealed class CharacterActionDamageAttributeScaleJsonDto
    {
        public int AttributeId { get; set; }
        public float Scale { get; set; }
    }

    public sealed class AnimationTrackEventJsonDto
    {
        public int Frame { get; set; }
        public int EndFrame { get; set; }
        public float PlaybackSpeed { get; set; } = 1f;
        public string Kind { get; set; } = string.Empty;
        public string AnimationActionKey { get; set; } = string.Empty;
        public string AnimationClipResourceKey { get; set; } = string.Empty;
        public int AnimationStartOffsetFrames { get; set; }
        public int AnimationEndOffsetFrames { get; set; }
        public float TransitionSeconds { get; set; }
        public int StableEventId { get; set; }
        public int StableEventOrder { get; set; }
    }

    public sealed class PresentationTrackEventJsonDto
    {
        public int Frame { get; set; }
        public int EndFrame { get; set; }
        public string Kind { get; set; } = string.Empty;
        public int CueId { get; set; }
        public string ResourceKey { get; set; } = string.Empty;
        public MxAttachmentTransform VfxAttachment { get; set; } = MxAttachmentTransform.Default;
        public string VfxPlaybackMode { get; set; } = CharacterActionVfxPlaybackMode.PlayToCompletion.ToString();
        public float VfxPlaybackRate { get; set; } = 1f;
        public float VfxScaleMultiplier { get; set; } = 1f;
        public int VfxStartOffsetFrames { get; set; }
        public int VfxEndOffsetFrames { get; set; }
        public string VfxNaturalEndPolicy { get; set; } = CharacterActionVfxStopPolicy.Complete.ToString();
        public int VfxParameter0 { get; set; }
        public int VfxParameter1 { get; set; }
        public int VfxParameter2 { get; set; }
        public int VfxParameter3 { get; set; }
        public int VfxDefinitionId { get; set; }
        public int NumericSocketId { get; set; }
        public bool BladeTrailGenerated { get; set; }
        public bool UseWeaponBladeTrailStyle { get; set; }
        public bool ShouldSerializeUseWeaponBladeTrailStyle() => UseWeaponBladeTrailStyle;
        public int BladeTrailSourceEventId { get; set; }
        public bool ShouldSerializeBladeTrailGenerated() => BladeTrailGenerated;
        public bool ShouldSerializeBladeTrailSourceEventId() => BladeTrailGenerated;
        public int StableEventId { get; set; }
        public int StableEventOrder { get; set; }

        public bool ShouldSerializeEndFrame()
        {
            return EndFrame != Frame;
        }

        public bool ShouldSerializeCueId()
        {
            return string.Equals(
                Kind,
                CharacterActionTrackEventKind.PlayAudioCue.ToString(),
                StringComparison.Ordinal);
        }

        public bool ShouldSerializeResourceKey() => IsVisualCue();
        public bool ShouldSerializeVfxAttachment() => IsVisualCue();
        public bool ShouldSerializeVfxPlaybackMode() => IsVisualCue();
        public bool ShouldSerializeVfxPlaybackRate() => IsVisualCue();
        public bool ShouldSerializeVfxScaleMultiplier() => IsVisualCue();
        public bool ShouldSerializeVfxStartOffsetFrames() => IsVisualCue();
        public bool ShouldSerializeVfxEndOffsetFrames() => IsVisualCue();
        public bool ShouldSerializeVfxNaturalEndPolicy() => IsVisualCue();
        public bool ShouldSerializeVfxParameter0() => IsVisualCue();
        public bool ShouldSerializeVfxParameter1() => IsVisualCue();
        public bool ShouldSerializeVfxParameter2() => IsVisualCue();
        public bool ShouldSerializeVfxParameter3() => IsVisualCue();
        public bool ShouldSerializeVfxDefinitionId() => IsVisualCue();
        public bool ShouldSerializeNumericSocketId() => IsVisualCue();

        private bool IsVisualCue()
        {
            return string.Equals(
                Kind,
                CharacterActionTrackEventKind.SpawnVisualCue.ToString(),
                StringComparison.Ordinal);
        }
    }

    public sealed class DebugTrackEventJsonDto
    {
        public int Frame { get; set; }
        public string Kind { get; set; } = string.Empty;
        public string MarkerId { get; set; } = string.Empty;
        public int StableEventId { get; set; }
        public int StableEventOrder { get; set; }
    }
}

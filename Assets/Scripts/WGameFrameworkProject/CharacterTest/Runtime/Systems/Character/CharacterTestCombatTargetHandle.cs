using System;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal readonly struct CharacterTestCombatTargetHandle
    {
        internal CharacterTestCombatTargetHandle(
            int sourceEntityId,
            int targetEntityId,
            GameplayEntityId targetGameplayEntityId,
            CombatEntityId targetCombatEntityId,
            int damageZoneId,
            int hitZoneId,
            int logicalHurtboxId,
            int bindingRevisionAtSelection,
            RuntimeFrame selectedFrame,
            string damageZoneLabel = "",
            string hitZoneLabel = "",
            string logicalHurtboxLabel = "")
        {
            SourceEntityId = sourceEntityId;
            TargetEntityId = targetEntityId;
            TargetGameplayEntityId = targetGameplayEntityId;
            TargetCombatEntityId = targetCombatEntityId;
            DamageZoneId = damageZoneId;
            DamageZoneLabel = damageZoneLabel ?? string.Empty;
            HitZoneId = hitZoneId;
            HitZoneLabel = hitZoneLabel ?? string.Empty;
            LogicalHurtboxId = logicalHurtboxId;
            LogicalHurtboxLabel = logicalHurtboxLabel ?? string.Empty;
            BindingRevisionAtSelection = bindingRevisionAtSelection;
            SelectedFrame = selectedFrame;
        }

        internal int SourceEntityId { get; }
        internal int TargetEntityId { get; }
        internal GameplayEntityId TargetGameplayEntityId { get; }
        internal CombatEntityId TargetCombatEntityId { get; }
        internal int DamageZoneId { get; }
        internal string DamageZoneLabel { get; }
        internal int HitZoneId { get; }
        internal string HitZoneLabel { get; }
        internal int LogicalHurtboxId { get; }
        internal string LogicalHurtboxLabel { get; }
        internal int BindingRevisionAtSelection { get; }
        internal RuntimeFrame SelectedFrame { get; }

        internal bool HasTarget => SourceEntityId > 0
            && TargetEntityId > 0
            && TargetGameplayEntityId.Index > 0
            && !TargetCombatEntityId.IsNone
            && LogicalHurtboxId > 0;

        internal bool SameActor(in CharacterTestCombatTargetHandle other) =>
            HasTarget
            && other.HasTarget
            && TargetGameplayEntityId.Equals(other.TargetGameplayEntityId);

        internal bool SameActor(GameplayEntityId gameplayEntityId) =>
            HasTarget && TargetGameplayEntityId.Equals(gameplayEntityId);

        internal bool SameSubtarget(in CharacterTestCombatTargetHandle other) =>
            SameActor(other)
            && LogicalHurtboxId == other.LogicalHurtboxId;
    }

    internal enum CharacterTestLockedHurtboxAvailabilityStatus
    {
        Unavailable = 0,
        Available = 1,
        SourceUnavailable = 2,
        TargetUnavailable = 3,
        HurtboxUnavailable = 4,
        InvalidShape = 5
    }

    internal readonly struct CharacterTestLockedHurtboxDistanceSample
    {
        internal CharacterTestLockedHurtboxDistanceSample(
            CharacterTestLockedHurtboxAvailabilityStatus status,
            RuntimeFrame evaluationFrame,
            RuntimeFrame geometryFrame,
            long geometryGeneration,
            int targetHurtboxRevision,
            long surfaceGapMillimeters,
            Fix64 surfaceGap,
            FixVector3 sourceWitnessPoint,
            FixVector3 targetWitnessPoint,
            FixVector3 aimPoint,
            CombatColliderId targetCombatColliderId,
            CombatPhysicsShapeKind targetShapeKind)
        {
            Status = status;
            EvaluationFrame = evaluationFrame;
            GeometryFrame = geometryFrame;
            GeometryGeneration = geometryGeneration;
            TargetHurtboxRevision = targetHurtboxRevision;
            SurfaceGapMillimeters = surfaceGapMillimeters < 0L ? 0L : surfaceGapMillimeters;
            SurfaceGap = surfaceGap < Fix64.Zero ? Fix64.Zero : surfaceGap;
            SourceWitnessPoint = sourceWitnessPoint;
            TargetWitnessPoint = targetWitnessPoint;
            AimPoint = aimPoint;
            TargetCombatColliderId = targetCombatColliderId;
            TargetShapeKind = targetShapeKind;
        }

        internal CharacterTestLockedHurtboxAvailabilityStatus Status { get; }
        internal bool HasDistance => Status == CharacterTestLockedHurtboxAvailabilityStatus.Available;
        internal RuntimeFrame EvaluationFrame { get; }
        internal RuntimeFrame GeometryFrame { get; }
        internal long GeometryGeneration { get; }
        internal int TargetHurtboxRevision { get; }
        internal long SurfaceGapMillimeters { get; }
        internal float SurfaceGapMeters => SurfaceGapMillimeters * 0.001f;
        internal Fix64 SurfaceGap { get; }
        internal FixVector3 SourceWitnessPoint { get; }
        internal FixVector3 TargetWitnessPoint { get; }
        internal FixVector3 AimPoint { get; }
        internal CombatColliderId TargetCombatColliderId { get; }
        internal CombatPhysicsShapeKind TargetShapeKind { get; }
    }
}

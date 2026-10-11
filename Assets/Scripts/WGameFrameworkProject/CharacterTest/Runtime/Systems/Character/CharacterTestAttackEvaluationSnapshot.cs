using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Immutable authority boundary for one CharacterTest attack evaluation.</summary>
    internal readonly struct CharacterTestAttackEvaluationSnapshot
    {
        internal CharacterTestAttackEvaluationSnapshot(
            RuntimeFrame simulationFrame,
            int combatPhysicsRevision,
            CombatEntityId sourceEntityId,
            GameplayEntityId sourceGameplayEntityId,
            FixVector3 sourcePosition,
            int sourceYawMilliDegrees,
            int sourcePoseRevision,
            int sourceFacingRevision,
            CombatEntityId targetEntityId,
            GameplayEntityId targetGameplayEntityId,
            int targetHurtboxRevision,
            int resolvedActionRevision)
        {
            SimulationFrame = simulationFrame;
            CombatPhysicsRevision = combatPhysicsRevision;
            SourceEntityId = sourceEntityId;
            SourceGameplayEntityId = sourceGameplayEntityId;
            SourcePosition = sourcePosition;
            SourceYawMilliDegrees = sourceYawMilliDegrees;
            SourcePoseRevision = sourcePoseRevision;
            SourceFacingRevision = sourceFacingRevision;
            TargetEntityId = targetEntityId;
            TargetGameplayEntityId = targetGameplayEntityId;
            TargetHurtboxRevision = targetHurtboxRevision;
            ResolvedActionRevision = resolvedActionRevision;
        }

        public RuntimeFrame SimulationFrame { get; }
        public int CombatPhysicsRevision { get; }
        public CombatEntityId SourceEntityId { get; }
        public GameplayEntityId SourceGameplayEntityId { get; }
        public FixVector3 SourcePosition { get; }
        public int SourceYawMilliDegrees { get; }
        public int SourcePoseRevision { get; }
        public int SourceFacingRevision { get; }
        public CombatEntityId TargetEntityId { get; }
        public GameplayEntityId TargetGameplayEntityId { get; }
        public int TargetHurtboxRevision { get; }
        public int ResolvedActionRevision { get; }

        /// <summary>
        /// Captures an evaluation snapshot using the source entity's
        /// authoritative write revisions. Callers cannot accidentally replace
        /// those revisions with the current simulation frame.
        /// </summary>
        internal static bool TryCapture(
            RuntimeFrame simulationFrame,
            CombatPhysicsWorld physicsWorld,
            CharacterTestCharacterEntity source,
            CharacterTestCharacterEntity target,
            CharacterTestDamageGeometrySnapshot targetGeometry,
            int resolvedActionRevision,
            out CharacterTestAttackEvaluationSnapshot snapshot)
        {
            if (source == null)
            {
                snapshot = default;
                return false;
            }

            return TryCapture(
                simulationFrame,
                physicsWorld,
                source,
                target,
                targetGeometry,
                source.SourcePoseRevision,
                source.SourceFacingRevision,
                resolvedActionRevision,
                out snapshot);
        }

        internal static bool TryCapture(
            RuntimeFrame simulationFrame,
            CombatPhysicsWorld physicsWorld,
            CharacterTestCharacterEntity source,
            CharacterTestCharacterEntity target,
            CharacterTestDamageGeometrySnapshot targetGeometry,
            int sourcePoseRevision,
            int sourceFacingRevision,
            int resolvedActionRevision,
            out CharacterTestAttackEvaluationSnapshot snapshot)
        {
            snapshot = default;
            if (physicsWorld == null
                || source == null
                || target == null
                || !IsLiveEntity(source)
                || !IsLiveEntity(target)
                || targetGeometry == null
                || !targetGeometry.IsValid
                || targetGeometry.Frame != simulationFrame
                || targetGeometry.WorldRevision != physicsWorld.Revision)
            {
                return false;
            }

            CombatEntityId targetEntityId = new CombatEntityId(
                CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + target.Id.Value);
            if (targetGeometry.CombatEntityId.Value != targetEntityId.Value)
            {
                return false;
            }

            if (targetGeometry.EntityId != target.Id.Value
                || !targetGeometry.GameplayEntityId.Equals(target.GameplayEntityId))
            {
                return false;
            }

            snapshot = new CharacterTestAttackEvaluationSnapshot(
                simulationFrame,
                physicsWorld.Revision,
                new CombatEntityId(CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + source.Id.Value),
                source.GameplayEntityId,
                new FixVector3(
                    CharacterTestDeterministicUnits.MillimetersToFix64Meters(source.Transform.XMillimeters),
                    CharacterTestDeterministicUnits.MillimetersToFix64Meters(source.Transform.YMillimeters),
                    CharacterTestDeterministicUnits.MillimetersToFix64Meters(source.Transform.ZMillimeters)),
                source.Transform.YawMilliDegrees,
                sourcePoseRevision,
                sourceFacingRevision,
                targetEntityId,
                target.GameplayEntityId,
                targetGeometry.HurtboxRevision,
                resolvedActionRevision);
            return true;
        }

        /// <summary>Strict no-allocation validation against the completed current simulation state.</summary>
        internal bool IsCurrent(
            RuntimeFrame simulationFrame,
            CombatPhysicsWorld physicsWorld,
            CharacterTestCharacterEntity source,
            CharacterTestCharacterEntity target,
            CharacterTestDamageGeometrySnapshot targetGeometry,
            int sourcePoseRevision,
            int sourceFacingRevision,
            int resolvedActionRevision)
        {
            if (physicsWorld == null
                || source == null
                || target == null
                || !IsLiveEntity(source)
                || !IsLiveEntity(target)
                || targetGeometry == null
                || !targetGeometry.IsValid)
            {
                return false;
            }

            return SimulationFrame == simulationFrame
                && CombatPhysicsRevision == physicsWorld.Revision
                && targetGeometry.Frame == simulationFrame
                && targetGeometry.WorldRevision == physicsWorld.Revision
                && SourceEntityId.Value == CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + source.Id.Value
                && TargetEntityId.Value == CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + target.Id.Value
                && SourceGameplayEntityId.Equals(source.GameplayEntityId)
                && TargetGameplayEntityId.Equals(target.GameplayEntityId)
                && SourceYawMilliDegrees == source.Transform.YawMilliDegrees
                && SourcePosition.Equals(new FixVector3(
                    CharacterTestDeterministicUnits.MillimetersToFix64Meters(source.Transform.XMillimeters),
                    CharacterTestDeterministicUnits.MillimetersToFix64Meters(source.Transform.YMillimeters),
                    CharacterTestDeterministicUnits.MillimetersToFix64Meters(source.Transform.ZMillimeters)))
                && SourcePoseRevision == sourcePoseRevision
                && SourceFacingRevision == sourceFacingRevision
                && TargetHurtboxRevision == targetGeometry.HurtboxRevision
                && ResolvedActionRevision == resolvedActionRevision
                && targetGeometry.EntityId == target.Id.Value
                && targetGeometry.GameplayEntityId.Equals(TargetGameplayEntityId)
                && targetGeometry.CombatEntityId.Value == TargetEntityId.Value;
        }

        private static bool IsLiveEntity(CharacterTestCharacterEntity entity)
        {
            return entity != null
                && entity.GameplayEntityId.IsValid
                && entity.Presence.IsActive;
        }
    }
}

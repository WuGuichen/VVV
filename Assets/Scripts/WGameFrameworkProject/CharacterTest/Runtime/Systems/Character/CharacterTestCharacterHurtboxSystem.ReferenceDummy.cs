using System;
using System.Collections.Generic;
using MxFramework.CharacterAction;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestCharacterHurtboxSystem
    {
        private readonly Dictionary<int, CharacterActionReferenceDummy> _referenceDummies = new Dictionary<int, CharacterActionReferenceDummy>();

        /// <remarks>LowFreqAlloc. Only configured by a measurement session before its first Simulation frame.</remarks>
        internal void ConfigureReferenceDummy(int entityId, CharacterActionReferenceDummy dummy)
        {
            if (entityId <= 0) throw new ArgumentOutOfRangeException(nameof(entityId));
            _referenceDummies.Add(entityId, dummy ?? throw new ArgumentNullException(nameof(dummy)));
        }

        private void SyncReferenceDummy(CharacterTestCharacterEntity entity, CharacterTestCharacterPhysicsRuntime physics,
            RuntimeFrame frame, CombatBodyId bodyId, CharacterActionReferenceDummy dummy)
        {
            CombatEntityId entityId = physics.GetEntityId(entity);
            CharacterTestDamageGeometrySnapshot geometry = BeginGeometryWrite(entity, entityId, 1);
            FixVector3 root = new FixVector3(Meters(entity.Transform.XMillimeters), Meters(entity.Transform.YMillimeters), Meters(entity.Transform.ZMillimeters));
            FixVector3 center = new FixVector3(Meters(dummy.CenterXMillimeters), Meters(dummy.CenterYMillimeters), Meters(dummy.CenterZMillimeters));
            // Identity must preserve the exact measured capsule. CORDIC approximates cos(0) as
            // 1.000002, which otherwise scales an offset center and invalidates its content contract.
            if (entity.Transform.YawMilliDegrees != 0)
            {
                CharacterTestDeterministicMath.SinCosMilliDegrees(entity.Transform.YawMilliDegrees, out int sin, out int cos);
                center = Rotate(center, TrigToFix64(sin), TrigToFix64(cos));
            }
            Fix64 radius = Meters(dummy.RadiusMillimeters);
            Fix64 segmentHalf = Meters(dummy.HeightMillimeters) / Fix64.FromInt(2) - radius;
            FixVector3 vertical = new FixVector3(Fix64.Zero, segmentHalf, Fix64.Zero);
            CombatPhysicsShape shape = CombatPhysicsShape.Capsule(center - vertical, center + vertical, radius);
            var colliderId = new CombatColliderId(CharacterTestCharacterPhysicsRuntime.CharacterHurtboxColliderIdStart);
            physics.World.UpsertBody(new CombatPhysicsBody(entityId, bodyId, root));
            physics.World.BeginCompoundColliderUpdate(bodyId, 1);
            try
            {
                physics.World.UpsertCompoundCollider(new CombatPhysicsCompoundLeaf(colliderId,
                    CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer, shape, priority: 0));
                physics.World.EndCompoundColliderUpdate();
            }
            catch
            {
                physics.World.CancelCompoundColliderUpdate();
                throw;
            }
            GetWorldAabb(shape, root, out FixVector3 min, out FixVector3 max);
            geometry.AddCollider(
                CharacterTestAuthoredContentIds.D400_Id36,
                "reference-capsule",
                colliderId,
                CharacterTestHitZoneDefaults.BodyPartId,
                string.Empty,
                CharacterTestHitZoneDefaults.HitZoneId,
                string.Empty,
                0,
                CharacterTestCharacterPhysicsRuntime.CharacterHurtboxLayer,
                shape,
                min,
                max);
            geometry.Commit(frame, physics.World.Revision, NextHurtboxRevision(entity.Id.Value), physics.World.TopologyRevision);
            _registeredColliderCounts[entity.Id.Value] = 1;
            _runtimeGeometryBindings.Remove(entity.Id.Value);
            BuildZoneSnapshot(entity.Id.Value, geometry, null);
        }

        private static Fix64 Meters(int millimeters) => Fix64.FromRatio(millimeters, 1000);
    }
}

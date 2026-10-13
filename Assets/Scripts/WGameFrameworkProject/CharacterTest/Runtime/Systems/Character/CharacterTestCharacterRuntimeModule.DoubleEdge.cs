using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestCharacterRuntimeModule
    {
        /// <remarks>NoAlloc. One sum per live target, immediately before the existing collision motion consumer.</remarks>
        private void ApplyDoubleEdgeFields(RuntimeFrame frame)
        {
            var effects = _world.DoubleEdgeState;
            for (int i = 0; i < _world.Entities.Count; i++) _world.Entities[i].BindThirdWorld(_world);
            if (!effects.HasState) return;
            for (int i = 0; i < _world.Entities.Count; i++) {
                var entity = _world.Entities[i];
                if (!entity.CombatState.IsAlive) { effects.RevokePair(entity.GameplayEntityId); effects.ThirdActorDeparted(entity.GameplayEntityId, frame.Value); continue; }
                if (!effects.TryPull(frame.Value, entity.Transform.XMillimeters, entity.Transform.YMillimeters,
                    entity.Transform.ZMillimeters, entity.Physics.EffectiveMassGrams, entity.Physics.EffectivePushScaleMilli,
                    entity.Physics.CanBePushed, entity.HorizontalVelocityXMillimetersPerSecond,
                    entity.HorizontalVelocityYMillimetersPerSecond, _simulationTickRate, out var force)
                    || force.X == 0 && force.Z == 0) continue;
                entity.EnqueueMotionRequest(CharacterTestCharacterMotionRequest.ExternalForceRaw(force.X, 0, force.Z,
                    CharacterTestCharacterMotionRequestSpace.World, default, 0, bypassGroundDamping: true));
            }
        }
    }
}

using System.Collections.Generic;
using MxFramework.Combat.Projectiles;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    internal sealed partial class TacticalDoubleEdgeBattleState
    {
        private readonly struct ThirdProjectileRequest
        {
            internal readonly CombatProjectileSpawnRequest Request;
            internal readonly TacticalThirdBatchAttack Attack;
            internal ThirdProjectileRequest(CombatProjectileSpawnRequest request, TacticalThirdBatchAttack attack)
            { Request = request; Attack = attack; }
        }
        private readonly List<ThirdProjectileRequest> _thirdProjectileRequests = new List<ThirdProjectileRequest>();
        internal bool RememberThirdProjectile(in CombatProjectileSpawnRequest request, in TacticalThirdBatchAttack attack)
        {
            if (!attack.IsValid) return true;
            foreach (var old in _thirdProjectileRequests)
                if (old.Request.Equals(request)) return old.Attack.Matches(attack);
            if (attack.Stage != _stage || request.SpawnFrame.Value < attack.AcceptedOrder.Frame) return false;
            // The same accepted requests have one projection regardless of producer callback arrival order.
            // A full-descriptor node collision is rejected before mutation, never broken by arrival order.
            ulong node = PendingNode(request, 0);
            int at = _thirdProjectileRequests.Count;
            for (int i = 0; i < _thirdProjectileRequests.Count; i++) {
                ulong other = PendingNode(_thirdProjectileRequests[i].Request, 0);
                if (other == node) return false;
                if (other > node && at == _thirdProjectileRequests.Count) at = i;
            }
            _thirdProjectileRequests.Insert(at, new ThirdProjectileRequest(request, attack)); _revision++; return true;
        }
        internal bool CompleteThirdProjectile(in CombatProjectileSpawnEvent birth)
        {
            if (!birth.HasRequest) return true;
            for (int i = 0; i < _thirdProjectileRequests.Count; i++) {
                var pending = _thirdProjectileRequests[i];
                if (!pending.Request.Equals(birth.Request)) continue;
                var p = birth.Projectile;
                var root = ProjectileSource(p.ProjectileId, p.SpawnSequence);
                ulong node = root.IsValid ? ProducerNode(1, p.ProjectileId, p.SpawnSequence, root.ActionRevision) : 0;
                if (!BindThirdSource(1, p.ProjectileId, p.SpawnSequence, pending.Attack, p.SpawnFrame.Value, node)) return false;
                _thirdProjectileRequests.RemoveAt(i); _revision++; return true;
            }
            return true;
        }
        private void ForgetThirdProjectiles(long frame)
        {
            for (int i = _thirdProjectileRequests.Count - 1; i >= 0; i--)
                if (_thirdProjectileRequests[i].Request.SpawnFrame.Value <= frame) { _thirdProjectileRequests.RemoveAt(i); _revision++; }
        }
        private void WriteThirdProjectileRequests(ref TacticalThirdBatchStateWriter writer)
        {
            writer.Value(_thirdProjectileRequests.Count);
            foreach (var row in _thirdProjectileRequests) {
                var q = row.Request;
                writer.Attack(row.Attack); writer.Value(q.SpawnFrame.Value); writer.Value(q.DefinitionId); writer.Value(q.SourceEntityId.Value);
                writer.Value(q.Origin.X.RawValue); writer.Value(q.Origin.Y.RawValue); writer.Value(q.Origin.Z.RawValue);
                writer.Value(q.Direction.X.RawValue); writer.Value(q.Direction.Y.RawValue); writer.Value(q.Direction.Z.RawValue);
                writer.Value(q.HasTargetPoint ? 1 : 0); writer.Value(q.TargetPoint.X.RawValue); writer.Value(q.TargetPoint.Y.RawValue); writer.Value(q.TargetPoint.Z.RawValue);
                writer.Value(q.ActionId); writer.Value(q.ActionInstanceId); writer.Value(q.TraceId); writer.Value(q.LocalFrame); writer.Value(q.SourceOrder);
                writer.Value(q.EffectiveLifetimeFrames); writer.Value(q.HasTrackingTarget ? 1 : 0); writer.Value(q.TrackingTargetEntityId.Value);
            }
        }
    }
}

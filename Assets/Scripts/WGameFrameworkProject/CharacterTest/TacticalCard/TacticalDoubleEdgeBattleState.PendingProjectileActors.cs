using System.Collections.Generic;
using MxFramework.Combat.Projectiles;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    internal sealed partial class TacticalDoubleEdgeBattleState
    {
        private readonly List<PendingProjectileActor> _pendingProjectileActors = new List<PendingProjectileActor>();
        private readonly struct PendingProjectileActor
        {
            internal readonly ulong Node; internal readonly CombatProjectileSpawnRequest Request; internal readonly ProjectileActor Actor;
            internal PendingProjectileActor(ulong node, CombatProjectileSpawnRequest request, ProjectileActor actor)
            { Node = node; Request = request; Actor = actor; }
        }
        internal bool RememberProjectileActor(in CombatProjectileSpawnRequest request, ProjectileActor actor)
        {
            if (actor == null || !actor.Actor.IsValid || actor.CombatSource != request.SourceEntityId.Value) return false;
            ulong node = PendingNode(request, 0); int at = 0;
            foreach (var row in _pendingProjectileActors) {
                if (row.Node == node) return row.Request.Equals(request) && row.Actor.Matches(actor);
                if (row.Node < node) at++;
            }
            _pendingProjectileActors.Insert(at, new PendingProjectileActor(node, request, actor)); _revision++; return true;
        }
        internal bool CompletePendingProjectileActor(in CombatProjectileSpawnEvent birth)
        {
            if (!birth.HasRequest) return false;
            for (int i = 0; i < _pendingProjectileActors.Count; i++) {
                var row = _pendingProjectileActors[i]; if (!row.Request.Equals(birth.Request)) continue;
                if (!FreezeProjectileActor(row.Actor.WithIdentity(birth.Projectile.ProjectileId, birth.Projectile.SpawnSequence))) return false;
                _pendingProjectileActors.RemoveAt(i); _revision++; return true;
            }
            return false;
        }
        internal ProjectileActor FindAncestorProjectileActor(in TacticalSecondBatchSource source)
        {
            var cursor = source;
            // Walk only the exact accepted ancestry of this commit; never choose the actor's latest projectile.
            while (cursor.IsValid) {
                foreach (var binding in _producerBindings) if (binding.Source.Matches(cursor)) {
                    if (binding.Kind == 1) return FindProjectileActor((int)binding.Id, (int)binding.Generation);
                    if (binding.Kind == 3 && FindTimelineActor((int)binding.Id) is ProjectileActor actor) return actor;
                }
                if (cursor.Parent == 0) return null;
                var leaf = Find(cursor.Commit)?.SecondBatch; bool found = false;
                if (leaf != null) for (int i = 0; i < leaf.SourceCount; i++) if (leaf.SourceAt(i).Node == cursor.Parent) {
                    cursor = leaf.SourceAt(i); found = true; break;
                }
                if (!found) return null;
            }
            return null;
        }
        private static long[] PendingActorRow(in PendingProjectileActor p)
        {
            var q = p.Request; var a = p.Actor;
            var row = new long[27 + a.AttributeCount * 2];
            row[0] = 15; row[1] = unchecked((long)p.Node); row[2] = q.SpawnFrame.Value; row[3] = q.DefinitionId;
            row[4] = q.SourceEntityId.Value; row[5] = q.Origin.X.RawValue; row[6] = q.Origin.Y.RawValue; row[7] = q.Origin.Z.RawValue;
            row[8] = q.Direction.X.RawValue; row[9] = q.Direction.Y.RawValue; row[10] = q.Direction.Z.RawValue;
            row[11] = q.HasTargetPoint ? 1 : 0; row[12] = q.TargetPoint.X.RawValue; row[13] = q.TargetPoint.Y.RawValue; row[14] = q.TargetPoint.Z.RawValue;
            row[15] = q.EffectiveLifetimeFrames; row[16] = q.HasTrackingTarget ? 1 : 0; row[17] = q.TrackingTargetEntityId.Value;
            row[18] = q.ActionId; row[19] = q.ActionInstanceId; row[20] = q.TraceId; row[21] = q.LocalFrame; row[22] = q.SourceOrder;
            row[23] = a.CombatSource; row[24] = a.Team; row[25] = a.Actor.Index; row[26] = a.Actor.Generation;
            for (int i = 0; i < a.AttributeCount; i++) { row[27 + 2 * i] = a.AttributeAt(i).AttributeId; row[28 + 2 * i] = a.AttributeAt(i).CurrentValue; }
            return row;
        }
        private void AddPendingActorHash(ref RuntimeHashAccumulator hash)
        {
            hash.BeginArray(11, _pendingProjectileActors.Count);
            for (int i = 0; i < _pendingProjectileActors.Count; i++) {
                var p = _pendingProjectileActors[i]; var q = p.Request; var a = p.Actor; hash.BeginElement(i);
                hash.AddLong(1, unchecked((long)p.Node)); hash.AddLong(2, q.SpawnFrame.Value); hash.AddInt(3, q.DefinitionId);
                hash.AddInt(4, q.SourceEntityId.Value); hash.AddLong(5, q.Origin.X.RawValue); hash.AddLong(6, q.Origin.Y.RawValue); hash.AddLong(7, q.Origin.Z.RawValue);
                hash.AddLong(8, q.Direction.X.RawValue); hash.AddLong(9, q.Direction.Y.RawValue); hash.AddLong(10, q.Direction.Z.RawValue);
                hash.AddBool(11, q.HasTargetPoint); hash.AddLong(12, q.TargetPoint.X.RawValue); hash.AddLong(13, q.TargetPoint.Y.RawValue); hash.AddLong(14, q.TargetPoint.Z.RawValue);
                hash.AddInt(15, q.EffectiveLifetimeFrames); hash.AddBool(16, q.HasTrackingTarget); hash.AddInt(17, q.TrackingTargetEntityId.Value);
                hash.AddInt(18, q.ActionId); hash.AddInt(19, q.ActionInstanceId); hash.AddInt(20, q.TraceId); hash.AddInt(21, q.LocalFrame); hash.AddInt(22, q.SourceOrder);
                hash.AddInt(23, a.CombatSource); hash.AddInt(24, a.Team); hash.AddInt(25, a.Actor.Index); hash.AddInt(26, a.Actor.Generation);
                hash.BeginArray(27, a.AttributeCount);
                for (int j = 0; j < a.AttributeCount; j++) { hash.BeginElement(j); hash.AddInt(1, a.AttributeAt(j).AttributeId); hash.AddInt(2, a.AttributeAt(j).CurrentValue); hash.EndElement(); }
                hash.EndArray(); hash.EndElement();
            }
            hash.EndArray();
        }
        private void AppendPendingActorProjection(List<long[]> rows)
        { foreach (var pending in _pendingProjectileActors) rows.Add(PendingActorRow(pending)); }
        private void ForgetResolvedPendingActors(long frame)
        {
            for (int i = _pendingProjectileActors.Count - 1; i >= 0; i--) if (_pendingProjectileActors[i].Request.SpawnFrame.Value <= frame)
            { _pendingProjectileActors.RemoveAt(i); _revision++; }
        }
    }
}

using System;
using System.Collections.Generic;
using MxFramework.Combat.Projectiles;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    internal sealed partial class TacticalDoubleEdgeBattleState
    {
        private readonly List<PendingProjectile> _pendingAccessoryProjectiles = new List<PendingProjectile>();
        private readonly struct PendingProjectile
        {
            internal readonly CombatProjectileSpawnRequest Request;
            internal readonly TacticalDirectAttackStamp Root;
            internal readonly TacticalSecondBatchSource Parent;
            internal readonly ulong Node;
            internal readonly bool Direct;
            internal PendingProjectile(CombatProjectileSpawnRequest request, TacticalDirectAttackStamp root,
                TacticalSecondBatchSource parent, ulong node, bool direct)
            { Request = request; Root = root; Parent = parent; Node = node; Direct = direct; }
        }

        internal bool RememberProjectileRequest(in CombatProjectileSpawnRequest request, in TacticalDirectAttackStamp root,
            in TacticalSecondBatchSource parent, long createdFrame, bool direct, in TacticalSecondBatchHitKey acceptedParent = default)
        {
            if (!root.IsValid) return true;
            if (SuppressesAttack(root) || request.SpawnFrame.Value < createdFrame) return false;
            var known = FindSource(root.Commit, root.ActionInstance, root.ActionRevision);
            if (!known.IsValid || known.CreatedFrame != root.CreatedFrame) return false;
            ulong node = PendingNode(request, parent.Node);
            foreach (var pending in _pendingAccessoryProjectiles) {
                // Native birth callbacks carry this exact request, so two parents may not claim it.
                if (pending.Request.Equals(request) && pending.Node != node) return false;
                if (pending.Node != node) continue;
                return pending.Request.Equals(request) && pending.Root.Commit.Matches(root.Commit)
                    && pending.Root.ActionInstance == root.ActionInstance && pending.Root.ActionRevision == root.ActionRevision
                    && pending.Root.CreatedFrame == root.CreatedFrame && pending.Direct == direct
                    && (acceptedParent.IsValid ? pending.Parent.Parent == parent.Node && pending.Parent.CausalTrigger.CompareTo(acceptedParent) == 0
                        : !pending.Parent.CausalTrigger.IsValid && (pending.Parent.Matches(parent) || !pending.Parent.IsValid && !parent.IsValid));
            }
            var durableParent = parent;
            if (acceptedParent.IsValid && !TryRegisterSecondBatchBirth(parent, node, parent.Producer,
                createdFrame, acceptedParent, out durableParent)) return false;
            int at = _pendingAccessoryProjectiles.Count;
            while (at > 0 && _pendingAccessoryProjectiles[at - 1].Node > node) at--;
            _pendingAccessoryProjectiles.Insert(at, new PendingProjectile(request, root, durableParent, node, direct));
            _revision++; return true;
        }

        internal bool CompleteProjectileRequest(in CombatProjectileSpawnEvent birth)
        {
            if (!birth.HasRequest) return true;
            for (int i = 0; i < _pendingAccessoryProjectiles.Count; i++) {
                var pending = _pendingAccessoryProjectiles[i];
                if (!pending.Request.Equals(birth.Request)) continue;
                var projectile = birth.Projectile;
                if (!BindProducer(1, projectile.ProjectileId, projectile.SpawnSequence, pending.Root,
                    pending.Parent, projectile.SpawnFrame.Value)) return false;
                _projectiles.Add(new ProjectileBirth(projectile.ProjectileId, projectile.SpawnSequence, pending.Root, 1, pending.Direct));
                _pendingAccessoryProjectiles.RemoveAt(i); _revision++; return true;
            }
            return true;
        }

        internal void ForgetResolvedProjectileRequests(long frame)
        {
            // The native queue has drained every due request, including rejected spawns. Future births retain their exact parent.
            ForgetResolvedPendingActors(frame);
            ForgetThirdProjectiles(frame);
            for (int i = _pendingAccessoryProjectiles.Count - 1; i >= 0; i--)
                if (_pendingAccessoryProjectiles[i].Request.SpawnFrame.Value <= frame) { _pendingAccessoryProjectiles.RemoveAt(i); _revision++; }
        }

        internal bool IsDirectProjectile(int id, int sequence)
        {
            foreach (var birth in _projectiles)
                if (birth.Kind == 1 && birth.Projectile == id && birth.Sequence == sequence) return birth.Direct;
            return true;
        }

        private static ulong PendingNode(in CombatProjectileSpawnRequest q, ulong parent)
        {
            unchecked {
                ulong value = (14695981039346656037UL ^ parent) * 1099511628211UL;
                value = (value ^ (ulong)(q.SpawnFrame.Value)) * 1099511628211UL;
                value = (value ^ (ulong)(q.DefinitionId)) * 1099511628211UL;
                value = (value ^ (ulong)(q.SourceEntityId.Value)) * 1099511628211UL;
                value = (value ^ (ulong)(q.Origin.X.RawValue)) * 1099511628211UL;
                value = (value ^ (ulong)(q.Origin.Y.RawValue)) * 1099511628211UL;
                value = (value ^ (ulong)(q.Origin.Z.RawValue)) * 1099511628211UL;
                value = (value ^ (ulong)(q.Direction.X.RawValue)) * 1099511628211UL;
                value = (value ^ (ulong)(q.Direction.Y.RawValue)) * 1099511628211UL;
                value = (value ^ (ulong)(q.Direction.Z.RawValue)) * 1099511628211UL;
                value = (value ^ (ulong)(q.HasTargetPoint ? 1 : 0)) * 1099511628211UL;
                value = (value ^ (ulong)(q.TargetPoint.X.RawValue)) * 1099511628211UL;
                value = (value ^ (ulong)(q.TargetPoint.Y.RawValue)) * 1099511628211UL;
                value = (value ^ (ulong)(q.TargetPoint.Z.RawValue)) * 1099511628211UL;
                value = (value ^ (ulong)(q.EffectiveLifetimeFrames)) * 1099511628211UL;
                value = (value ^ (ulong)(q.HasTrackingTarget ? 1 : 0)) * 1099511628211UL;
                value = (value ^ (ulong)(q.TrackingTargetEntityId.Value)) * 1099511628211UL;
                value = (value ^ (ulong)(q.ActionId)) * 1099511628211UL;
                value = (value ^ (ulong)(q.ActionInstanceId)) * 1099511628211UL;
                value = (value ^ (ulong)(q.TraceId)) * 1099511628211UL;
                value = (value ^ (ulong)(q.LocalFrame)) * 1099511628211UL;
                value = (value ^ (ulong)(q.SourceOrder)) * 1099511628211UL;
                return value | (1UL << 63);
            }
        }

        private void AddPendingProjectileHash(ref RuntimeHashAccumulator hash)
        {
            hash.BeginArray(7, _pendingAccessoryProjectiles.Count);
            for (int i = 0; i < _pendingAccessoryProjectiles.Count; i++) {
                var p = _pendingAccessoryProjectiles[i]; var q = p.Request; var k = p.Root.Commit;
                hash.BeginElement(i);
                hash.AddLong(1, q.SpawnFrame.Value);
                hash.AddLong(2, q.DefinitionId);
                hash.AddLong(3, q.SourceEntityId.Value);
                hash.AddLong(4, q.Origin.X.RawValue);
                hash.AddLong(5, q.Origin.Y.RawValue);
                hash.AddLong(6, q.Origin.Z.RawValue);
                hash.AddLong(7, q.Direction.X.RawValue);
                hash.AddLong(8, q.Direction.Y.RawValue);
                hash.AddLong(9, q.Direction.Z.RawValue);
                hash.AddLong(10, q.HasTargetPoint ? 1 : 0);
                hash.AddLong(11, q.TargetPoint.X.RawValue);
                hash.AddLong(12, q.TargetPoint.Y.RawValue);
                hash.AddLong(13, q.TargetPoint.Z.RawValue);
                hash.AddLong(14, q.EffectiveLifetimeFrames);
                hash.AddLong(15, q.HasTrackingTarget ? 1 : 0);
                hash.AddLong(16, q.TrackingTargetEntityId.Value);
                hash.AddLong(17, q.ActionId);
                hash.AddLong(18, q.ActionInstanceId);
                hash.AddLong(19, q.TraceId);
                hash.AddLong(20, q.LocalFrame);
                hash.AddLong(21, q.SourceOrder);
                hash.AddLong(22, unchecked((long)p.Node)); hash.AddLong(23, unchecked((long)p.Parent.Node));
                hash.AddBool(24, p.Direct); hash.AddLong(25, unchecked((long)k.Stage)); hash.AddLong(26, unchecked((long)k.Commit));
                hash.AddLong(27, unchecked((long)k.Execution)); hash.AddInt(28, k.Actor.Index); hash.AddInt(29, k.Actor.Generation);
                hash.AddLong(30, unchecked((long)k.BattleCardInstance)); hash.AddLong(31, k.Frame);
                hash.AddInt(32, p.Root.ActionInstance); hash.AddLong(33, unchecked((long)p.Root.ActionRevision)); hash.AddLong(34, p.Root.CreatedFrame);
                hash.EndElement();
            }
            hash.EndArray();
        }

        private void AppendPendingProjectileProjection(List<long[]> rows)
        {
            foreach (var p in _pendingAccessoryProjectiles) {
                var q = p.Request; var k = p.Root.Commit;
                rows.Add(new long[] { 11, q.SpawnFrame.Value, q.DefinitionId, q.SourceEntityId.Value, q.Origin.X.RawValue, q.Origin.Y.RawValue, q.Origin.Z.RawValue, q.Direction.X.RawValue, q.Direction.Y.RawValue, q.Direction.Z.RawValue, q.HasTargetPoint ? 1 : 0, q.TargetPoint.X.RawValue, q.TargetPoint.Y.RawValue, q.TargetPoint.Z.RawValue, q.EffectiveLifetimeFrames, q.HasTrackingTarget ? 1 : 0, q.TrackingTargetEntityId.Value, q.ActionId, q.ActionInstanceId, q.TraceId, q.LocalFrame, q.SourceOrder,
                    unchecked((long)p.Node), unchecked((long)p.Parent.Node), p.Direct ? 1 : 0,
                    unchecked((long)k.Stage), unchecked((long)k.Commit), unchecked((long)k.Execution), k.Actor.Index, k.Actor.Generation,
                    unchecked((long)k.BattleCardInstance), k.Frame, p.Root.ActionInstance, unchecked((long)p.Root.ActionRevision), p.Root.CreatedFrame });
            }
        }
    }
}

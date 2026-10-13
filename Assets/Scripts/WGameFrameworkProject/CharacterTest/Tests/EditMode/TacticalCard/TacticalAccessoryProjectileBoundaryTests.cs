using System;
using MxFramework.Combat.Core;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Physics;
using MxFramework.Combat.Projectiles;
using MxFramework.Core.Math;
using NUnit.Framework;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    public sealed class TacticalAccessoryProjectileBoundaryTests
    {
        [TestCase(false)][TestCase(true)]
        public void NativeSpawnIdentityExistsBeforeImpactAndPerProjectileTeamPolicyWins(bool friendly)
        {
            var physics = new CombatPhysicsWorld();
            physics.UpsertBody(new CombatPhysicsBody(new CombatEntityId(2), new CombatBodyId(2), new FixVector3(Fix64.Half, Fix64.Zero, Fix64.Zero)));
            physics.UpsertAabbCollider(new CombatPhysicsAabbCollider(new CombatBodyId(2), new CombatColliderId(1), 1,
                new FixVector3(-Fix64.FromRatio(1, 10), -Fix64.FromRatio(1, 10), -Fix64.FromRatio(1, 10)),
                new FixVector3(Fix64.FromRatio(1, 10), Fix64.FromRatio(1, 10), Fix64.FromRatio(1, 10))));
            var queue = new CombatProjectileSpawnRequestBuffer(); var observer = new Observer { Friendly = friendly };
            var definitions = new CombatProjectileDefinitionRegistry(new[] { new CombatProjectileDefinition(1, "boundary.projectile",
                new CombatProjectileMotionConfig(CombatProjectileMotionKind.Linear, Fix64.Half, 10),
                new CombatProjectileCollisionConfig(Fix64.FromRatio(1, 10), CombatPhysicsLayerMask.FromLayer(1),
                    stopOnFirstAcceptedImpact: true, stopOnBlockedImpact: true, maxAcceptedImpacts: 1),
                new CombatProjectileImpactConfig(presentationProfileId: 1, stableEventId: 2)) });
            var runtime = new CombatProjectileRuntime(definitions, queue, physics, observer, new Teams(!friendly), allowFriendlyFire: false);
            var request = new CombatProjectileSpawnRequest(new CombatFrame(0), 1, new CombatEntityId(1), FixVector3.Zero,
                new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero), actionId: 10, actionInstanceId: 20, traceId: 30, localFrame: 0);
            queue.Enqueue(request); var birth = runtime.Tick(new CombatFrame(0));
            Assert.AreEqual(1, observer.Births); Assert.AreEqual(1, birth.Spawns.Count);
            Assert.AreEqual(request, birth.Spawns[0].Request); Assert.AreEqual(observer.Id, birth.Spawns[0].Projectile.ProjectileId);
            var frozen = runtime.CreateStateSnapshot();
            Assert.IsTrue(runtime.TryRestoreStateSnapshot(frozen, out var issue), issue);
            Assert.AreEqual(1, observer.Births, "Restore must not publish another birth or arm another source.");
            var hit = runtime.Tick(new CombatFrame(1));
            Assert.Greater(observer.PayloadCalls, 0); Assert.Greater(observer.RelationCalls, 0);
            Assert.AreEqual(1, hit.Impacts.Count);
            Assert.AreEqual(friendly ? HitResolveKind.Friendly : HitResolveKind.Damage, hit.Impacts[0].ResolveResult.Kind);
        }
        private sealed class Observer : ICombatProjectileImpactResolver, ICombatProjectileSpawnObserver, ICombatProjectileImpactRelations
        {
            internal bool Friendly; internal int Births, Id, Sequence, PayloadCalls, RelationCalls;
            public void OnProjectileSpawned(in CombatProjectileSpawnEvent birth)
            { Assert.IsTrue(birth.HasRequest); Births++; Id = birth.Projectile.ProjectileId; Sequence = birth.Projectile.SpawnSequence; }
            public ITeamRelationProvider ResolveTeamRelations(in CombatProjectileSnapshot projectile, ITeamRelationProvider fallback)
            { Assert.AreEqual(Id, projectile.ProjectileId); Assert.AreEqual(Sequence, projectile.SpawnSequence); RelationCalls++; return new Teams(Friendly); }
            public bool TryResolveImpact(CombatProjectileSnapshot p, CombatQueryResult hit, CombatFrame frame, CombatProjectileImpactContext context, out CombatProjectileImpactPayload payload)
            { Assert.AreEqual(1, Births); Assert.AreEqual(Id, p.ProjectileId); PayloadCalls++;
                payload = new CombatProjectileImpactPayload(10, 0, FixVector3.Zero, HitTargetStateFlags.Alive, presentationProfileId: "1", stableEventId: "2"); return true; }
        }
        private sealed class Teams : ITeamRelationProvider
        {
            private readonly bool _friendly; internal Teams(bool friendly) => _friendly = friendly;
            public bool AreHostile(CombatEntityId a, CombatEntityId b) => !_friendly;
            public bool AreFriendly(CombatEntityId a, CombatEntityId b) => _friendly;
            public bool IsSameTeam(CombatEntityId a, CombatEntityId b) => _friendly;
        }
    }
}

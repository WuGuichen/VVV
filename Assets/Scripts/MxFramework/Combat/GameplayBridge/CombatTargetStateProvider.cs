using System;
using MxFramework.Combat.Hit;
using MxFramework.Gameplay;

namespace MxFramework.Combat.GameplayBridge
{
    public sealed class CombatTargetStateProvider
    {
        private readonly GameplayStatusId _invincibleStatusId;
        private readonly GameplayStatusId _blockingStatusId;
        private readonly GameplayStatusId _parryingStatusId;
        private readonly GameplayStatusId _superArmorStatusId;
        private readonly GameplayStatusId _guardBrokenStatusId;

        public CombatTargetStateProvider(
            GameplayStatusId invincibleStatusId = default,
            GameplayStatusId blockingStatusId = default,
            GameplayStatusId parryingStatusId = default,
            GameplayStatusId superArmorStatusId = default,
            GameplayStatusId guardBrokenStatusId = default)
        {
            _invincibleStatusId = invincibleStatusId;
            _blockingStatusId = blockingStatusId;
            _parryingStatusId = parryingStatusId;
            _superArmorStatusId = superArmorStatusId;
            _guardBrokenStatusId = guardBrokenStatusId;
        }

        public HitTargetStateFlags Evaluate(GameplayComponentWorld world, GameplayEntityId entityId)
        {
            if (world == null)
                throw new ArgumentNullException(nameof(world));

            if (!world.TryGetStore(out GameplayComponentStore<GameplayLifecycleComponent> lifecycleStore) ||
                !lifecycleStore.TryGet(entityId, out GameplayLifecycleComponent lifecycle))
            {
                return HitTargetStateFlags.None;
            }

            HitTargetStateFlags flags = HitTargetStateFlags.None;
            if (lifecycle.IsAlive)
                flags |= HitTargetStateFlags.Alive;

            if (!world.TryGetStore(out GameplayComponentStore<GameplayStatusComponent> statusStore) ||
                !statusStore.TryGet(entityId, out GameplayStatusComponent statuses))
            {
                return flags;
            }

            if (_invincibleStatusId.IsValid && statuses.Contains(_invincibleStatusId))
                flags |= HitTargetStateFlags.Invincible;

            bool isGuardBroken = _guardBrokenStatusId.IsValid && statuses.Contains(_guardBrokenStatusId);
            if (!isGuardBroken && _blockingStatusId.IsValid && statuses.Contains(_blockingStatusId))
                flags |= HitTargetStateFlags.Blocking;

            if (_parryingStatusId.IsValid && statuses.Contains(_parryingStatusId))
                flags |= HitTargetStateFlags.Parrying;

            if (_superArmorStatusId.IsValid && statuses.Contains(_superArmorStatusId))
                flags |= HitTargetStateFlags.SuperArmor;

            return flags;
        }
    }
}

using System;
using MxFramework.CharacterAction;
using MxFramework.Combat.Core;
using MxFramework.Combat.Projectiles;
using MxFramework.Game.CharacterTest.TacticalCard;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestCharacterCombatTraceSystem : IAbilityEffectTimelineStartGate
    {
        private TacticalDirectAttackStamp _timelineBirthRoot;
        private TacticalThirdBatchAttack _timelineThirdBirth;
        private TacticalSecondBatchSource _timelineBirthParent;
        private TacticalSecondBatchHitKey _timelineBirthReceipt;
        private TacticalSecondBatchHit _projectileCausalParent;

        internal bool AccessoryTimelinesIdle => _effectTimelineRuntime == null || _effectTimelineRuntime.IsIdle;
        internal bool RestoreAccessoryTimelineIdentity(int next)
            => _effectTimelineRuntime != null ? _effectTimelineRuntime.TryRestoreIdleIdentity(next) : next == 1;

        private bool AllowsProjectileAttack(in CombatProjectileSnapshot projectile)
        {
            var owner = _entityLookup.AccessoryState;
            return owner == null || !owner.SuppressesAttack(owner.ProjectileSource(projectile.ProjectileId, projectile.SpawnSequence));
        }

        private CharacterTestAccessoryHitEvidence ProjectilePeriodicEvidence(int id, int generation,
            CharacterTestCharacterEntity source, CombatEntityId targetId, ulong occurrence)
        {
            var owner = _entityLookup.AccessoryState;
            var root = owner?.ProjectileSource(id, generation) ?? default;
            var lineage = owner?.SecondBatchProducer(root, 1, id, generation) ?? default;
            var target = _entityLookup.FindEntityByCombatId(targetId);
            var actor = owner?.FindProjectileActor(id, generation);
            return CharacterTestAccessoryHitEvidence.Periodic.WithSource(root).WithCollision(lineage.IsValid ? lineage.Producer
                : actor?.Actor ?? source?.GameplayEntityId ?? default, target,
                target != null && (actor != null ? actor.Team != target.Team.TeamId : source != null && source.Team.TeamId != target.Team.TeamId), lineage, occurrence);
        }

        private void BindProjectileChildProducer(int kind, long id, in CombatProjectileSnapshot projectile, RuntimeFrame frame)
        {
            var owner = _entityLookup.AccessoryState;
            if (owner == null) return;
            var root = owner.ProjectileSource(projectile.ProjectileId, projectile.SpawnSequence);
            var parent = owner.SecondBatchProducer(root, 1, projectile.ProjectileId, projectile.SpawnSequence);
            var trigger = parent.IsValid && _projectileCausalParent.Source.Matches(parent) ? _projectileCausalParent.Key : default;
            if (!owner.BindProducer(kind, id, 0, root, parent, frame.Value, trigger))
                throw new InvalidOperationException("accessory:ProjectileChildBirthRejected");
        }

        private AbilityEffectTimelineStartResult StartAccessoryTimeline(AbilityEffectTimelineContext context, RuntimeFrame frame,
            TacticalDirectAttackStamp root, TacticalSecondBatchSource parent, TacticalSecondBatchHitKey receipt = default, TacticalThirdBatchAttack third = default)
        {
            _timelineBirthRoot = root; _timelineBirthParent = parent; _timelineBirthReceipt = receipt; _timelineThirdBirth = third;
            try { return _effectTimelineRuntime.StartTimeline(context, frame); }
            finally { _timelineBirthRoot = default; _timelineBirthParent = default; _timelineBirthReceipt = default; _timelineThirdBirth = default; }
        }

        public bool TryAcceptTimelineStart(int instanceId, in AbilityEffectTimelineContext context, RuntimeFrame frame)
        {
            var authority = _entityLookup.AccessoryState;
            if (authority == null) return true;
            var root = _timelineBirthRoot; var parent = _timelineBirthParent; var receipt = _timelineBirthReceipt;
            if (context.ParentInstanceId > 0) {
                var birth = authority.FindProducer(3, context.ParentInstanceId, 0);
                root = birth.Root; parent = birth.Source; receipt = default;
            }
            var actor = context.ParentInstanceId > 0 ? authority.FindTimelineActor(context.ParentInstanceId) : null;
            actor ??= authority.FindAncestorProjectileActor(parent);
            actor ??= CaptureProducerActor(_entityLookup.FindEntity(context.SourceEntityId));
            if (authority.Stage != 0) {
                if (instanceId <= 0 || instanceId == int.MaxValue || root.IsValid && actor == null) return false;
                var existingActor = authority.FindTimelineActor(instanceId);
                if (existingActor != null && (actor == null || !existingActor.Matches(actor))) return false;
            }
            if (!authority.BindProducer(3, instanceId, 0, root, parent, frame.Value, receipt)) return false;
            if (authority.Stage != 0 && actor != null && !authority.FreezeTimelineActor(instanceId, actor)) return false;
            var third = context.ParentInstanceId > 0 ? authority.ThirdSource(3, context.ParentInstanceId, 0).Attack : _timelineThirdBirth;
            if (!authority.BindThirdSource(3, instanceId, 0, third, frame.Value)) return false;
            authority.RecordTimelineIdentity(checked(instanceId + 1));
            return true;
        }

        private CharacterTestAccessoryHitEvidence ProducerEvidence(int kind, long id, long generation,
            CharacterTestCharacterEntity source, CombatEntityId targetId, ulong occurrence,
            CharacterTestAccessoryHitEvidence evidence = default)
        {
            var binding = _entityLookup.AccessoryState?.FindProducer(kind, id, generation) ?? default;
            var target = _entityLookup.FindEntityByCombatId(targetId);
            var actor = kind == 3 ? _entityLookup.AccessoryState?.FindTimelineActor((int)id) : null;
            return evidence.WithSource(binding.Root).WithCollision(binding.Source.IsValid ? binding.Source.Producer
                : actor?.Actor ?? source?.GameplayEntityId ?? default, target,
                target != null && (actor != null ? actor.Team != target.Team.TeamId : source != null && source.Team.TeamId != target.Team.TeamId), binding.Source, occurrence);
        }

        private CharacterTestAccessoryHitEvidence ActionRequestEvidence(CharacterTestCharacterEntity source,
            CharacterActionGameplayRequest request, CombatEntityId targetId, RuntimeFrame frame, ulong occurrence,
            CharacterTestAccessoryHitEvidence evidence = default)
        {
            var authority = _entityLookup.AccessoryState;
            var root = source.CaptureDoubleEdgeSource(request.Metadata.InstanceId);
            long id = (long)(TacticalDoubleEdgeBattleState.ProducerNode(9, StablePositiveId(request.Metadata.InstanceId),
                StablePositiveId(request.Metadata.StableEventId), ((ulong)(uint)source.GameplayEntityId.Index << 32)
                    | (uint)source.GameplayEntityId.Generation) & long.MaxValue);
            if (id == 0) throw new InvalidOperationException("accessory:ActionProducerIdentityExhausted");
            if (authority != null && !authority.BindProducer(9, id, frame.Value, root,
                authority.SecondBatchRoot(root), frame.Value)) throw new InvalidOperationException("accessory:ActionProducerBirthRejected");
            return ProducerEvidence(9, id, frame.Value, source, targetId, occurrence, evidence);
        }
    }
}

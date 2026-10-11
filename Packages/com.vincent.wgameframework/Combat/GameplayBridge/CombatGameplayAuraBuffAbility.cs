using System;
using MxFramework.Gameplay;

namespace MxFramework.Combat.GameplayBridge
{
    /// <summary>
    /// Component-native ability that starts or refreshes a caster-following aura source.
    /// </summary>
    public sealed class CombatGameplayAuraBuffAbility : IGameplayComponentAbility
    {
        public const string DurationOverflowReason = "CombatGameplayAuraDurationOverflow";

        private static readonly GameplayEntityId[] EmptyTargets = new GameplayEntityId[0];

        public CombatGameplayAuraBuffAbility(
            int abilityId,
            int auraId,
            long durationFrames,
            bool isPermanent = false,
            GameplayComponentAbilityRuleSet rules = null)
        {
            if (abilityId <= 0)
                throw new ArgumentOutOfRangeException(nameof(abilityId), "Combat gameplay aura ability id must be greater than zero.");
            if (auraId <= 0)
                throw new ArgumentOutOfRangeException(nameof(auraId), "Combat gameplay aura id must be greater than zero.");
            if (durationFrames < 0L)
                throw new ArgumentOutOfRangeException(nameof(durationFrames), "Combat gameplay aura duration frames cannot be negative.");
            if (!isPermanent && durationFrames <= 0L)
                throw new ArgumentOutOfRangeException(nameof(durationFrames), "Non-permanent combat gameplay aura duration frames must be greater than zero.");

            AbilityId = abilityId;
            AuraId = auraId;
            DurationFrames = durationFrames;
            IsPermanent = isPermanent;
            Rules = rules ?? GameplayComponentAbilityRuleSet.Empty;
        }

        public int AbilityId { get; }
        public int AuraId { get; }
        public long DurationFrames { get; }
        public bool IsPermanent { get; }
        public GameplayComponentAbilityRuleSet Rules { get; }

        public GameplayComponentAbilityResult Cast(GameplayComponentAbilityContext context)
        {
            if (!TryResolveEndFrame(context.Frame.Value, out long endFrame))
            {
                return GameplayComponentAbilityResult.Failed(
                    AbilityId,
                    context.CasterEntityId,
                    GameplayComponentAbilityFailureCode.EffectFailed,
                    DurationOverflowReason,
                    EmptyTargets);
            }

            GameplayComponentStore<GameplayComponentAuraSourceSetComponent> store =
                context.World.GetOrCreateStore<GameplayComponentAuraSourceSetComponent>();
            store.TryGet(context.CasterEntityId, out GameplayComponentAuraSourceSetComponent current);
            GameplayComponentAuraSourceSetComponent updated = current.Upsert(
                new GameplayComponentAuraSourceEntry(
                    AuraId,
                    endFrame,
                    IsPermanent,
                    sourceId: AbilityId));
            store.Set(context.CasterEntityId, updated);

            return GameplayComponentAbilityResult.Succeeded(
                AbilityId,
                context.CasterEntityId,
                new[] { context.CasterEntityId });
        }

        private bool TryResolveEndFrame(long frame, out long endFrame)
        {
            if (IsPermanent)
            {
                endFrame = 0L;
                return true;
            }

            if (DurationFrames > long.MaxValue - frame)
            {
                endFrame = 0L;
                return false;
            }

            endFrame = frame + DurationFrames;
            return true;
        }
    }
}



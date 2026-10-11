using System;
using MxFramework.CharacterAction;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.CharacterControl
{
    public enum CharacterActionKind
    {
        None = 0,
        Attack = 1,
        Skill = 2,
        Interact = 3,
        Dodge = 4,
        GameplayAbility = 5,
        Reaction = 6
    }

    public readonly struct CharacterActionRequest : IEquatable<CharacterActionRequest>
    {
        public const int AttackIntentId = 53;
        public const int SkillIntentId = 54;
        public const int InteractIntentId = 55;
        public const int DodgeIntentId = 56;
        public const int GameplayAbilityIntentId = 57;

        public CharacterActionRequest(
            RuntimeFrame frame,
            int sourceId,
            CharacterControlEntityRef entity,
            CharacterActionKind kind,
            int combatActionId,
            int gameplayAbilityId,
            GameplayEntityId targetGameplayEntityId,
            bool forceStart,
            bool queueIfBusy,
            RuntimeTraceIdentity traceId = default,
            int actionIntentId = 0,
            CharacterActionSourceKind sourceKind = CharacterActionSourceKind.Command)
        {
            if (combatActionId < 0)
                throw new ArgumentOutOfRangeException(nameof(combatActionId), "Combat action id cannot be negative.");
            if (gameplayAbilityId < 0)
                throw new ArgumentOutOfRangeException(nameof(gameplayAbilityId), "Gameplay ability id cannot be negative.");

            Frame = frame;
            SourceId = sourceId;
            Entity = entity;
            Kind = kind;
            CombatActionId = combatActionId;
            GameplayAbilityId = gameplayAbilityId;
            TargetGameplayEntityId = targetGameplayEntityId;
            ForceStart = forceStart;
            QueueIfBusy = queueIfBusy;
            TraceId = traceId;
            ActionIntentId = actionIntentId;
            SourceKind = sourceKind == CharacterActionSourceKind.None
                ? CharacterActionSourceKind.Command
                : sourceKind;
        }

        public RuntimeFrame Frame { get; }

        public int SourceId { get; }

        public CharacterControlEntityRef Entity { get; }

        public CharacterActionKind Kind { get; }

        public int CombatActionId { get; }

        public int GameplayAbilityId { get; }

        public GameplayEntityId TargetGameplayEntityId { get; }

        public bool ForceStart { get; }

        public bool QueueIfBusy { get; }

        public RuntimeTraceIdentity TraceId { get; }

        public int ActionIntentId { get; }

        public CharacterActionSourceKind SourceKind { get; }

        public bool HasCombatAction => CombatActionId > 0;

        public bool HasGameplayAbility => GameplayAbilityId > 0;

        public bool HasTarget => TargetGameplayEntityId.IsValid;

        public static CharacterActionRequest CombatAction(
            RuntimeFrame frame,
            CharacterControlEntityRef entity,
            CharacterActionKind kind,
            int combatActionId,
            int sourceId = 0,
            RuntimeTraceIdentity traceId = default,
            bool forceStart = false,
            bool queueIfBusy = false,
            int actionIntentId = 0,
            CharacterActionSourceKind sourceKind = CharacterActionSourceKind.Command)
        {
            return new CharacterActionRequest(
                frame,
                sourceId,
                entity,
                kind,
                combatActionId,
                gameplayAbilityId: 0,
                targetGameplayEntityId: default,
                forceStart: forceStart,
                queueIfBusy: queueIfBusy,
                traceId: traceId,
                actionIntentId: actionIntentId,
                sourceKind: sourceKind);
        }

        public static CharacterActionRequest GameplayAbility(
            RuntimeFrame frame,
            CharacterControlEntityRef entity,
            int abilityId,
            GameplayEntityId target = default,
            int sourceId = 0,
            RuntimeTraceIdentity traceId = default,
            bool queueIfBusy = false,
            int actionIntentId = 0,
            CharacterActionSourceKind sourceKind = CharacterActionSourceKind.GameplayAbility)
        {
            return new CharacterActionRequest(
                frame,
                sourceId,
                entity,
                CharacterActionKind.GameplayAbility,
                combatActionId: 0,
                gameplayAbilityId: abilityId,
                targetGameplayEntityId: target,
                forceStart: false,
                queueIfBusy: queueIfBusy,
                traceId: traceId,
                actionIntentId: actionIntentId,
                sourceKind: sourceKind);
        }

        public CharacterActionRequest WithFrame(RuntimeFrame frame)
        {
            return new CharacterActionRequest(
                frame,
                SourceId,
                Entity,
                Kind,
                CombatActionId,
                GameplayAbilityId,
                TargetGameplayEntityId,
                ForceStart,
                QueueIfBusy,
                TraceId,
                ActionIntentId,
                SourceKind);
        }

        public bool Equals(CharacterActionRequest other)
        {
            return Frame == other.Frame
                && SourceId == other.SourceId
                && Entity.Equals(other.Entity)
                && Kind == other.Kind
                && CombatActionId == other.CombatActionId
                && GameplayAbilityId == other.GameplayAbilityId
                && TargetGameplayEntityId.Equals(other.TargetGameplayEntityId)
                && ForceStart == other.ForceStart
                && QueueIfBusy == other.QueueIfBusy
                && SourceKind == other.SourceKind
                && TraceId.Equals(other.TraceId)
                && ActionIntentId == other.ActionIntentId;
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterActionRequest other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Frame.GetHashCode();
                hash = (hash * 397) ^ SourceId;
                hash = (hash * 397) ^ Entity.GetHashCode();
                hash = (hash * 397) ^ (int)Kind;
                hash = (hash * 397) ^ CombatActionId;
                hash = (hash * 397) ^ GameplayAbilityId;
                hash = (hash * 397) ^ TargetGameplayEntityId.GetHashCode();
                hash = (hash * 397) ^ (ForceStart ? 1 : 0);
                hash = (hash * 397) ^ (QueueIfBusy ? 1 : 0);
                hash = (hash * 397) ^ (int)SourceKind;
                hash = (hash * 397) ^ TraceId.GetHashCode();
                hash = (hash * 397) ^ ActionIntentId;
                return hash;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using MxFramework.Runtime;

namespace MxFramework.Gameplay
{
    public interface IGameplayComponentAbility
    {
        int AbilityId { get; }
        GameplayComponentAbilityRuleSet Rules { get; }
        GameplayComponentAbilityResult Cast(GameplayComponentAbilityContext context);
    }

    public enum GameplayComponentAbilityFailureCode
    {
        None = 0,
        MissingCaster = 1,
        MissingAbility = 2,
        MissingAttributeSet = 3,
        MissingTarget = 4,
        EffectFailed = 5,
        InvalidCommandPayload = 6,
        OnCooldown = 7,
        InsufficientCost = 8,
        InvalidAbilityRule = 9
    }

    public enum GameplayComponentTargetMode
    {
        Self = 0,
        ExplicitSingle = 1
    }

    public readonly struct GameplayComponentAbilityContext
    {
        public GameplayComponentAbilityContext(
            RuntimeFrame frame,
            GameplayComponentWorld world,
            GameplayEntityId casterEntityId,
            IReadOnlyList<GameplayEntityId> targetEntityIds,
            string traceId,
            int commandId = 0)
        {
            Frame = frame;
            World = world ?? throw new ArgumentNullException(nameof(world));
            if (!casterEntityId.IsValid)
                throw new ArgumentException("Component ability caster entity id must be valid.", nameof(casterEntityId));

            CasterEntityId = casterEntityId;
            TargetEntityIds = targetEntityIds ?? Array.Empty<GameplayEntityId>();
            TraceId = traceId ?? string.Empty;
            CommandId = commandId == 0 ? GameplayRuntimeCommandIds.CastComponentAbility : commandId;
        }

        public RuntimeFrame Frame { get; }
        public GameplayComponentWorld World { get; }
        public GameplayEntityId CasterEntityId { get; }
        public IReadOnlyList<GameplayEntityId> TargetEntityIds { get; }
        public string TraceId { get; }
        public int CommandId { get; }
    }

    public sealed class GameplayComponentAbilityResult
    {
        private static readonly GameplayEntityId[] EmptyTargets = new GameplayEntityId[0];

        private GameplayComponentAbilityResult(
            bool success,
            int abilityId,
            GameplayEntityId casterEntityId,
            IReadOnlyList<GameplayEntityId> targetEntityIds,
            GameplayComponentAbilityFailureCode failureCode,
            string failureReason)
        {
            Success = success;
            AbilityId = abilityId;
            CasterEntityId = casterEntityId;
            TargetEntityIds = targetEntityIds ?? EmptyTargets;
            FailureCode = failureCode;
            FailureReason = failureReason ?? string.Empty;
        }

        public bool Success { get; }
        public int AbilityId { get; }
        public GameplayEntityId CasterEntityId { get; }
        public IReadOnlyList<GameplayEntityId> TargetEntityIds { get; }
        public GameplayComponentAbilityFailureCode FailureCode { get; }
        public string FailureReason { get; }

        public static GameplayComponentAbilityResult Succeeded(
            int abilityId,
            GameplayEntityId casterEntityId,
            IReadOnlyList<GameplayEntityId> targetEntityIds)
        {
            return new GameplayComponentAbilityResult(
                true,
                abilityId,
                casterEntityId,
                CopyTargets(targetEntityIds),
                GameplayComponentAbilityFailureCode.None,
                string.Empty);
        }

        public static GameplayComponentAbilityResult Failed(
            int abilityId,
            GameplayEntityId casterEntityId,
            GameplayComponentAbilityFailureCode failureCode,
            string failureReason,
            IReadOnlyList<GameplayEntityId> targetEntityIds = null)
        {
            if (failureCode == GameplayComponentAbilityFailureCode.None)
                throw new ArgumentException("Component ability failure code cannot be None for failed result.", nameof(failureCode));

            return new GameplayComponentAbilityResult(
                false,
                abilityId,
                casterEntityId,
                CopyTargets(targetEntityIds),
                failureCode,
                failureReason);
        }

        private static GameplayEntityId[] CopyTargets(IReadOnlyList<GameplayEntityId> targetEntityIds)
        {
            if (targetEntityIds == null || targetEntityIds.Count == 0)
                return EmptyTargets;

            var copy = new GameplayEntityId[targetEntityIds.Count];
            for (int i = 0; i < targetEntityIds.Count; i++)
                copy[i] = targetEntityIds[i];
            return copy;
        }
    }
}

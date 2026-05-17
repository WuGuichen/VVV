using System;
using MxFramework.AI;
using MxFramework.Gameplay;

namespace MxFramework.AI.GameplayBridge
{
    public sealed class PostureAiSensorAdapter : IAiSensor
    {
        private readonly GameplayComponentWorld _componentWorld;
        private readonly Func<int, GameplayEntityId> _selfResolver;
        private readonly Func<int, GameplayEntityId> _targetResolver;

        public PostureAiSensorAdapter(Func<int, GameplayEntityId> selfResolver)
            : this(null, selfResolver, null)
        {
        }

        public PostureAiSensorAdapter(
            Func<int, GameplayEntityId> selfResolver,
            Func<int, GameplayEntityId> targetResolver)
            : this(null, selfResolver, targetResolver)
        {
        }

        public PostureAiSensorAdapter(
            GameplayComponentWorld componentWorld,
            Func<int, GameplayEntityId> selfResolver)
            : this(componentWorld, selfResolver, null)
        {
        }

        public PostureAiSensorAdapter(
            GameplayComponentWorld componentWorld,
            Func<int, GameplayEntityId> selfResolver,
            Func<int, GameplayEntityId> targetResolver)
        {
            _componentWorld = componentWorld;
            _selfResolver = selfResolver ?? throw new ArgumentNullException(nameof(selfResolver));
            _targetResolver = targetResolver;
        }

        public void Sense(IAiAgent agent, IAiWorldState worldState)
        {
            if (_componentWorld == null)
                throw new InvalidOperationException("A GameplayComponentWorld must be supplied before sensing pressure facts.");

            Sense(agent, worldState, _componentWorld);
        }

        public void Sense(
            IAiAgent agent,
            IAiWorldState worldState,
            GameplayComponentWorld gameplayWorld)
        {
            if (agent == null)
                throw new ArgumentNullException(nameof(agent));
            if (worldState == null)
                throw new ArgumentNullException(nameof(worldState));
            if (gameplayWorld == null)
                throw new ArgumentNullException(nameof(gameplayWorld));

            WriteSubject(gameplayWorld, worldState, _selfResolver(agent.Id), FactSubject.Self);
            WriteSubject(gameplayWorld, worldState, _targetResolver == null ? default : _targetResolver(agent.Id), FactSubject.Target);
        }

        private static void WriteSubject(
            GameplayComponentWorld componentWorld,
            IAiWorldState worldState,
            GameplayEntityId entityId,
            FactSubject subject)
        {
            if (!entityId.IsValid || !componentWorld.IsAlive(entityId))
            {
                RemoveSubject(worldState, subject);
                return;
            }

            WritePosture(componentWorld, worldState, entityId, subject);
            WriteGuard(componentWorld, worldState, entityId, subject);
            WriteArmor(componentWorld, worldState, entityId, subject);
        }

        private static void WritePosture(
            GameplayComponentWorld componentWorld,
            IAiWorldState worldState,
            GameplayEntityId entityId,
            FactSubject subject)
        {
            if (!componentWorld.TryGetStore(out GameplayComponentStore<GameplayPosturePressureComponent> store)
                || !store.TryGet(entityId, out GameplayPosturePressureComponent component)
                || !component.HasValidState())
            {
                RemovePosture(worldState, subject);
                return;
            }

            worldState.SetValue(GetPostureBandKey(subject), (int)component.CurrentBand);
            worldState.SetValue(GetPostureRatioKey(subject), Ratio(component.CurrentPressure, component.MaxPressure));
            worldState.SetValue(GetPostureBrokenKey(subject), component.IsBroken);
        }

        private static void WriteGuard(
            GameplayComponentWorld componentWorld,
            IAiWorldState worldState,
            GameplayEntityId entityId,
            FactSubject subject)
        {
            if (!componentWorld.TryGetStore(out GameplayComponentStore<GameplayGuardPressureComponent> store)
                || !store.TryGet(entityId, out GameplayGuardPressureComponent component)
                || !component.HasValidState())
            {
                RemoveGuard(worldState, subject);
                return;
            }

            worldState.SetValue(GetGuardBandKey(subject), (int)component.CurrentBand);
            worldState.SetValue(GetGuardRatioKey(subject), Ratio(component.CurrentPressure, component.MaxPressure));
            worldState.SetValue(GetGuardBrokenKey(subject), component.IsBroken);
        }

        private static void WriteArmor(
            GameplayComponentWorld componentWorld,
            IAiWorldState worldState,
            GameplayEntityId entityId,
            FactSubject subject)
        {
            if (!componentWorld.TryGetStore(out GameplayComponentStore<GameplayArmorIntegrityComponent> store)
                || !store.TryGet(entityId, out GameplayArmorIntegrityComponent component)
                || !component.HasValidState())
            {
                RemoveArmor(worldState, subject);
                return;
            }

            worldState.SetValue(GetArmorRatioKey(subject), Ratio(component.CurrentIntegrity, component.MaxIntegrity));
            worldState.SetValue(GetArmorBrokenKey(subject), component.IsBroken);
        }

        private static float Ratio(int currentValue, int maxValue)
        {
            if (maxValue <= 0)
                return 0f;

            return (float)currentValue / maxValue;
        }

        private static void RemoveSubject(IAiWorldState worldState, FactSubject subject)
        {
            RemovePosture(worldState, subject);
            RemoveGuard(worldState, subject);
            RemoveArmor(worldState, subject);
        }

        private static void RemovePosture(IAiWorldState worldState, FactSubject subject)
        {
            worldState.Remove(GetPostureBandKey(subject));
            worldState.Remove(GetPostureRatioKey(subject));
            worldState.Remove(GetPostureBrokenKey(subject));
        }

        private static void RemoveGuard(IAiWorldState worldState, FactSubject subject)
        {
            worldState.Remove(GetGuardBandKey(subject));
            worldState.Remove(GetGuardRatioKey(subject));
            worldState.Remove(GetGuardBrokenKey(subject));
        }

        private static void RemoveArmor(IAiWorldState worldState, FactSubject subject)
        {
            worldState.Remove(GetArmorRatioKey(subject));
            worldState.Remove(GetArmorBrokenKey(subject));
        }

        private static AiFactKey GetPostureBandKey(FactSubject subject)
        {
            return subject == FactSubject.Self
                ? RuntimeAiGameplayPressureFactKeys.SelfPostureBand
                : RuntimeAiGameplayPressureFactKeys.TargetPostureBand;
        }

        private static AiFactKey GetPostureRatioKey(FactSubject subject)
        {
            return subject == FactSubject.Self
                ? RuntimeAiGameplayPressureFactKeys.SelfPostureRatio
                : RuntimeAiGameplayPressureFactKeys.TargetPostureRatio;
        }

        private static AiFactKey GetPostureBrokenKey(FactSubject subject)
        {
            return subject == FactSubject.Self
                ? RuntimeAiGameplayPressureFactKeys.SelfPostureBroken
                : RuntimeAiGameplayPressureFactKeys.TargetPostureBroken;
        }

        private static AiFactKey GetGuardBandKey(FactSubject subject)
        {
            return subject == FactSubject.Self
                ? RuntimeAiGameplayPressureFactKeys.SelfGuardBand
                : RuntimeAiGameplayPressureFactKeys.TargetGuardBand;
        }

        private static AiFactKey GetGuardRatioKey(FactSubject subject)
        {
            return subject == FactSubject.Self
                ? RuntimeAiGameplayPressureFactKeys.SelfGuardRatio
                : RuntimeAiGameplayPressureFactKeys.TargetGuardRatio;
        }

        private static AiFactKey GetGuardBrokenKey(FactSubject subject)
        {
            return subject == FactSubject.Self
                ? RuntimeAiGameplayPressureFactKeys.SelfGuardBroken
                : RuntimeAiGameplayPressureFactKeys.TargetGuardBroken;
        }

        private static AiFactKey GetArmorRatioKey(FactSubject subject)
        {
            return subject == FactSubject.Self
                ? RuntimeAiGameplayPressureFactKeys.SelfArmorRatio
                : RuntimeAiGameplayPressureFactKeys.TargetArmorRatio;
        }

        private static AiFactKey GetArmorBrokenKey(FactSubject subject)
        {
            return subject == FactSubject.Self
                ? RuntimeAiGameplayPressureFactKeys.SelfArmorBroken
                : RuntimeAiGameplayPressureFactKeys.TargetArmorBroken;
        }

        private enum FactSubject
        {
            Self,
            Target
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.Combat.Damage;
using MxFramework.Combat.Hit;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.Combat.GameplayBridge
{
    public sealed class CombatDamageApplicationSystem : IGameplaySystem
    {
        public const string DefaultSystemId = "mxframework.bridge.combat.damage_application";
        private const int TraceIdStringCacheInitialCapacity = 512;

        private readonly CombatEntityGameplayMap _entityMap;
        private readonly GameplayComponentWorld _componentWorld;
        private readonly GameplayGuardPressureSystem _guardPressureSystem;
        private readonly Func<IReadOnlyList<HitResolveResult>> _getHitResults;
        private readonly Func<HitResolveResult, DamageProfileResolveResult> _getDamagePayload;
        private readonly int _hpAttributeId;
        private readonly List<RuntimeCommand> _outputCommands;
        private readonly IList<DamageTelemetryRow> _telemetryRows;
        private readonly Action<DamageResolvedEvent> _publishDamageResolvedEvent;
        private readonly Action<HitPresentationEvent> _publishHitPresentationEvent;
        private readonly Action<ArmorBreakEvent> _publishArmorBreakEvent;
        private readonly Dictionary<int, string> _traceIdStrings =
            new Dictionary<int, string>(TraceIdStringCacheInitialCapacity);

        public CombatDamageApplicationSystem(
            CombatEntityGameplayMap entityMap,
            GameplayComponentWorld componentWorld,
            GameplayGuardPressureSystem guardPressureSystem,
            Func<IReadOnlyList<HitResolveResult>> getHitResults,
            Func<HitResolveResult, DamageProfileResolveResult> getDamagePayload,
            int hpAttributeId,
            List<RuntimeCommand> outputCommands,
            IList<DamageTelemetryRow> telemetryRows = null,
            Action<DamageResolvedEvent> publishDamageResolvedEvent = null,
            Action<HitPresentationEvent> publishHitPresentationEvent = null,
            Action<ArmorBreakEvent> publishArmorBreakEvent = null,
            string systemId = DefaultSystemId,
            GameplaySystemPhase phase = GameplaySystemPhase.Resolution,
            int priority = 60)
        {
            _entityMap = entityMap ?? throw new ArgumentNullException(nameof(entityMap));
            _componentWorld = componentWorld ?? throw new ArgumentNullException(nameof(componentWorld));
            _guardPressureSystem = guardPressureSystem;
            _getHitResults = getHitResults ?? throw new ArgumentNullException(nameof(getHitResults));
            _getDamagePayload = getDamagePayload ?? throw new ArgumentNullException(nameof(getDamagePayload));
            _hpAttributeId = hpAttributeId;
            _outputCommands = outputCommands ?? throw new ArgumentNullException(nameof(outputCommands));
            _telemetryRows = telemetryRows;
            _publishDamageResolvedEvent = publishDamageResolvedEvent;
            _publishHitPresentationEvent = publishHitPresentationEvent;
            _publishArmorBreakEvent = publishArmorBreakEvent;
            SystemId = systemId ?? string.Empty;
            Phase = phase;
            Priority = priority;
        }

        public string SystemId { get; }
        public GameplaySystemPhase Phase { get; }
        public int Priority { get; }
        public bool IsEnabled { get; private set; } = true;

        public void SetEnabled(bool enabled)
        {
            IsEnabled = enabled;
        }

        /// <remarks>NoAlloc on the steady-state path after registration/initialization; invalid-call and exception paths may allocate diagnostics.</remarks>
        public void Tick(GameplaySystemContext context)
        {
            if (!IsEnabled)
                return;

            IReadOnlyList<HitResolveResult> results = _getHitResults();
            if (results == null || results.Count == 0)
                return;

            for (int i = 0; i < results.Count; i++)
                Apply(context, results[i]);
        }

        private void Apply(GameplaySystemContext context, HitResolveResult result)
        {
            DamageProfileResolveResult payload = _getDamagePayload(result);
            bool hasGameplayTarget = _entityMap.TryGetGameplayId(result.TargetId, out GameplayEntityId targetEntityId);
            int hpBefore = 0;
            bool hasHpAttribute = hasGameplayTarget && TryReadCurrentAttribute(targetEntityId, _hpAttributeId, out hpBefore);
            int finalDamage = 0;

            if (hasGameplayTarget && result.Kind == HitResolveKind.Blocked)
            {
                EnqueueGuardPressure(targetEntityId, result, payload);
            }
            else if (hasGameplayTarget && hasHpAttribute && result.IsAcceptedDamage)
            {
                int armorAdjustedDamage = ApplyArmorIntegrity(context.Frame, targetEntityId, result);
                finalDamage = hpBefore > 0 ? Math.Min(armorAdjustedDamage, hpBefore) : 0;
                AppendHpDamageCommand(context, targetEntityId, finalDamage, result.TraceId);
            }

            int hpAfter = result.IsAcceptedDamage && hasHpAttribute
                ? Math.Max(0, hpBefore - finalDamage)
                : hpBefore;
            DamageTelemetryRow row = CreateTelemetryRow(context.Frame, result, payload, finalDamage, hpBefore, hpAfter);
            _telemetryRows?.Add(row);
            PublishEvents(result, payload, finalDamage);
        }

        private void EnqueueGuardPressure(
            GameplayEntityId entityId,
            HitResolveResult result,
            DamageProfileResolveResult payload)
        {
            if (_guardPressureSystem == null)
                return;

            int guardPressure = payload.GuardPressure > 0 ? payload.GuardPressure : result.Damage;
            if (guardPressure <= 0)
                return;

            _guardPressureSystem.Enqueue(new GameplayGuardPressureRequest(
                entityId,
                guardPressure,
                true,
                GetTraceId(result.TraceId)));
        }

        private int ApplyArmorIntegrity(RuntimeFrame frame, GameplayEntityId entityId, HitResolveResult result)
        {
            int incomingDamage = result.Damage;
            if (incomingDamage <= 0)
                return 0;

            if (!_componentWorld.TryGetStore(out GameplayComponentStore<GameplayArmorIntegrityComponent> armorStore) ||
                !armorStore.TryGet(entityId, out GameplayArmorIntegrityComponent armor) ||
                !HasUsableArmor(armor))
            {
                return incomingDamage;
            }

            int previousIntegrity = armor.CurrentIntegrity;
            long absorbed = (long)incomingDamage * previousIntegrity / armor.MaxIntegrity;
            int adjustedDamage = incomingDamage - (int)absorbed;
            armor.CurrentIntegrity = Math.Max(0, previousIntegrity - incomingDamage);
            bool brokeThisHit = previousIntegrity > 0 && armor.CurrentIntegrity == 0 && !armor.IsBroken;
            if (brokeThisHit)
                armor.IsBroken = true;

            armorStore.Set(entityId, armor);
            if (brokeThisHit)
                PublishArmorBreak(frame, entityId, previousIntegrity, armor, incomingDamage, result.TraceId);

            return adjustedDamage;
        }

        private void AppendHpDamageCommand(
            GameplaySystemContext context,
            GameplayEntityId entityId,
            int finalDamage,
            int traceId)
        {
            if (finalDamage <= 0)
                return;

            _outputCommands.Add(GameplayRuntimeCommandFactory.AddComponentAttribute(
                context.Frame,
                entityId,
                _hpAttributeId,
                -finalDamage,
                sourceId: 0,
                traceId: GetTraceId(traceId)));
        }

        private bool TryReadCurrentAttribute(GameplayEntityId entityId, int attributeId, out int currentValue)
        {
            if (attributeId <= 0
                || !_componentWorld.TryGetStore(out GameplayComponentStore<GameplayAttributeSetComponent> store)
                || !store.TryGet(entityId, out GameplayAttributeSetComponent attributes))
            {
                currentValue = 0;
                return false;
            }

            if (!attributes.TryGet(attributeId, out GameplayAttributeValue value))
            {
                currentValue = 0;
                return false;
            }

            currentValue = value.CurrentValue;
            return true;
        }

        private DamageTelemetryRow CreateTelemetryRow(
            RuntimeFrame frame,
            HitResolveResult result,
            DamageProfileResolveResult payload,
            int finalDamage,
            int hpBefore,
            int hpAfter)
        {
            DamageProfileUsage usage = payload.Usage;
            return new DamageTelemetryRow(
                frame.Value,
                usage.SourceKind,
                usage.ActionId,
                usage.ActionInstanceId,
                usage.StableEventId,
                payload.DamageProfileId,
                payload.DamageTypeId,
                usage.EquipmentSlotId,
                usage.WeaponId,
                usage.SourceEntityId > 0 ? usage.SourceEntityId : result.AttackerId.Value,
                usage.TargetEntityId > 0 ? usage.TargetEntityId : result.TargetId.Value,
                usage.HitZoneId,
                usage.BodyPartId,
                usage.LocalFrame,
                payload.IncomingDamage > 0 ? payload.IncomingDamage : result.Damage,
                result.IsAcceptedDamage ? finalDamage : 0,
                result.Kind,
                hpBefore,
                hpAfter,
                payload.FormulaTrace,
                hitPointWorld: result.HitPointWorld,
                hitNormalWorld: result.HitNormalWorld,
                analysisCategory: payload.AnalysisCategory);
        }

        private void PublishEvents(
            HitResolveResult result,
            DamageProfileResolveResult payload,
            int finalDamage)
        {
            if (_publishDamageResolvedEvent == null && _publishHitPresentationEvent == null)
                return;

            if (result.Kind != HitResolveKind.Damage && result.Kind != HitResolveKind.Blocked)
                return;

            var request = new DamageRequest(payload.DamageProfileId, payload.Usage);
            var evt = new DamageResolvedEvent(request, payload, result, finalDamage);
            _publishDamageResolvedEvent?.Invoke(evt);
            if (payload.PresentationProfileId > 0)
                _publishHitPresentationEvent?.Invoke(new HitPresentationEvent(evt, payload.PresentationProfileId));
        }

        private void PublishArmorBreak(
            RuntimeFrame frame,
            GameplayEntityId entityId,
            int previousIntegrity,
            GameplayArmorIntegrityComponent armor,
            int incomingDamage,
            int traceId)
        {
            if (_publishArmorBreakEvent == null)
                return;

            _publishArmorBreakEvent(new ArmorBreakEvent(
                frame,
                entityId,
                previousIntegrity,
                armor.CurrentIntegrity,
                armor.MaxIntegrity,
                incomingDamage,
                GetTraceId(traceId)));
        }

        private static bool HasUsableArmor(GameplayArmorIntegrityComponent armor)
        {
            return armor.MaxIntegrity > 0
                && armor.CurrentIntegrity > 0
                && armor.CurrentIntegrity <= armor.MaxIntegrity
                && !armor.IsBroken;
        }

        private const int DamageTraceKind = 100;
        private static RuntimeTraceIdentity GetTraceId(int traceId)
        {
            return new RuntimeTraceIdentity(DamageTraceKind, 0, traceId, 0);
        }
    }
}



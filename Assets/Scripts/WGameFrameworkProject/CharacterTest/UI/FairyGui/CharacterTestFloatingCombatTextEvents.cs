using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.Camera.URP;
using MxFramework.Combat.Damage;
using MxFramework.Combat.Hit;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using UnityEngine;
using Fgui = global::FairyGUI;
using UnityCamera = UnityEngine.Camera;

namespace MxFramework.Game.CharacterTest
{
    public enum CharacterTestFloatingCombatTextKind
    {
        Damage = 0,
        Blocked = 1,
        Invalid = 2,
        ScatterImpact = 3,
        CohesionBreak = 4
    }

    public readonly struct CharacterTestFloatingCombatTextStyle
    {
        public CharacterTestFloatingCombatTextStyle(
            Color color,
            int fontSize,
            float lifetimeSeconds,
            float risePixels,
            float driftPixels,
            float startScale = 1f,
            float endScale = 0.86f)
        {
            Color = color;
            FontSize = fontSize <= 0 ? 18 : fontSize;
            LifetimeSeconds = lifetimeSeconds <= 0f ? 0.85f : lifetimeSeconds;
            RisePixels = risePixels < 0f ? 0f : risePixels;
            DriftPixels = driftPixels < 0f ? 0f : driftPixels;
            StartScale = startScale <= 0f ? 1f : startScale;
            EndScale = endScale <= 0f ? StartScale : endScale;
        }

        public Color Color { get; }
        public int FontSize { get; }
        public float LifetimeSeconds { get; }
        public float RisePixels { get; }
        public float DriftPixels { get; }
        public float StartScale { get; }
        public float EndScale { get; }

        public static CharacterTestFloatingCombatTextStyle Damage =>
            new CharacterTestFloatingCombatTextStyle(
                new Color(1f, 0.24f, 0.16f, 1f),
                22,
                0.86f,
                46f,
                24f,
                1.08f,
                0.92f);

        public static CharacterTestFloatingCombatTextStyle Blocked =>
            new CharacterTestFloatingCombatTextStyle(
                new Color(0.72f, 0.62f, 0.44f, 1f),
                18,
                0.76f,
                34f,
                18f);

        public static CharacterTestFloatingCombatTextStyle Invalid =>
            new CharacterTestFloatingCombatTextStyle(
                new Color(0.58f, 0.61f, 0.58f, 1f),
                17,
                0.68f,
                28f,
                14f);

        public static CharacterTestFloatingCombatTextStyle ScatterImpact =>
            new CharacterTestFloatingCombatTextStyle(
                new Color(1f, 0.7f, 0.22f, 1f),
                18,
                0.82f,
                38f,
                20f);

        public static CharacterTestFloatingCombatTextStyle CohesionBreak =>
            new CharacterTestFloatingCombatTextStyle(
                new Color(1f, 0.86f, 0.36f, 1f),
                30,
                1.25f,
                72f,
                28f,
                1.35f,
                1.05f);
    }

    public readonly struct CharacterTestFloatingCombatTextEvent
    {
        public CharacterTestFloatingCombatTextEvent(
            RuntimeFrame frame,
            CharacterTestFloatingCombatTextKind kind,
            int sourceEntityId,
            int targetEntityId,
            string text,
            Vector3 worldPosition,
            int priority,
            CharacterTestFloatingCombatTextStyle style)
        {
            Frame = frame;
            Kind = kind;
            SourceEntityId = sourceEntityId < 0 ? 0 : sourceEntityId;
            TargetEntityId = targetEntityId < 0 ? 0 : targetEntityId;
            Text = text ?? string.Empty;
            WorldPosition = worldPosition;
            Priority = priority;
            Style = style;
        }

        public RuntimeFrame Frame { get; }
        public CharacterTestFloatingCombatTextKind Kind { get; }
        public int SourceEntityId { get; }
        public int TargetEntityId { get; }
        public string Text { get; }
        public Vector3 WorldPosition { get; }
        public int Priority { get; }
        public CharacterTestFloatingCombatTextStyle Style { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Text) && Style.LifetimeSeconds > 0f;
    }

    public static class CharacterTestFloatingCombatTextEventBuilder
    {
        private const float TargetFallbackHeightMeters = 1.8f;

        public static int AppendFrameEvents(
            GameSlice slice,
            IList<CharacterTestFloatingCombatTextEvent> output)
        {
            if (slice == null || output == null || slice.CombatTraceSystem == null)
                return 0;

            int startCount = output.Count;
            IReadOnlyList<CharacterTestCharacterEntity> entities = slice.CharacterEntities;
            AppendDamageEvents(slice.CombatTraceSystem.DamageTelemetryRows, entities, output);
            AppendHitPresentationEventsIfNeeded(
                slice.CombatTraceSystem.HitPresentationEvents,
                slice.CombatTraceSystem.DamageTelemetryRows,
                entities,
                output);
            AppendCohesionBreakEvents(slice.CombatTraceSystem.CohesionBreakEvents, entities, output);
            AppendScatterImpactEvents(slice.CombatTraceSystem.ScatterImpactResolvedEvents, entities, output);
            return output.Count - startCount;
        }

        public static bool TryCreateFromTelemetry(
            DamageTelemetryRow row,
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            out CharacterTestFloatingCombatTextEvent textEvent)
        {
            RuntimeFrame frame = new RuntimeFrame(row.RuntimeFrame);
            if (!TryResolveWorldPosition(row.HitPointWorld, row.TargetEntityId, entities, out Vector3 worldPosition))
            {
                textEvent = default;
                return false;
            }

            switch (row.ResultKind)
            {
                case HitResolveKind.Damage:
                    if (row.FinalDamage > 0)
                    {
                        textEvent = new CharacterTestFloatingCombatTextEvent(
                            frame,
                            CharacterTestFloatingCombatTextKind.Damage,
                            row.SourceEntityId,
                            row.TargetEntityId,
                            "-" + row.FinalDamage,
                            worldPosition,
                            80,
                            CharacterTestFloatingCombatTextStyle.Damage);
                        return true;
                    }

                    textEvent = new CharacterTestFloatingCombatTextEvent(
                        frame,
                        CharacterTestFloatingCombatTextKind.Invalid,
                        row.SourceEntityId,
                        row.TargetEntityId,
                        "0",
                        worldPosition,
                        35,
                        CharacterTestFloatingCombatTextStyle.Invalid);
                    return true;
                case HitResolveKind.Blocked:
                    textEvent = new CharacterTestFloatingCombatTextEvent(
                        frame,
                        CharacterTestFloatingCombatTextKind.Blocked,
                        row.SourceEntityId,
                        row.TargetEntityId,
                        "BLOCK",
                        worldPosition,
                        55,
                        CharacterTestFloatingCombatTextStyle.Blocked);
                    return true;
                case HitResolveKind.Invincible:
                    textEvent = CreateInvalid(frame, row.SourceEntityId, row.TargetEntityId, "IMMUNE", worldPosition);
                    return true;
                case HitResolveKind.Parried:
                    textEvent = CreateInvalid(frame, row.SourceEntityId, row.TargetEntityId, "PARRY", worldPosition);
                    return true;
                case HitResolveKind.TargetDead:
                    textEvent = CreateInvalid(frame, row.SourceEntityId, row.TargetEntityId, "DOWN", worldPosition);
                    return true;
                default:
                    textEvent = default;
                    return false;
            }
        }

        public static bool TryCreateFromHitPresentation(
            HitPresentationEvent hitEvent,
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            out CharacterTestFloatingCombatTextEvent textEvent)
        {
            DamageResolvedEvent damageEvent = hitEvent.DamageEvent;
            HitResolveResult result = damageEvent.HitResult;
            DamageProfileUsage usage = damageEvent.ProfileResult.Usage;
            var row = new DamageTelemetryRow(
                result.Frame.Value,
                usage.SourceKind,
                usage.ActionId,
                usage.ActionInstanceId,
                usage.StableEventId,
                damageEvent.ProfileResult.DamageProfileId,
                damageEvent.ProfileResult.DamageTypeId,
                usage.EquipmentSlotId,
                usage.WeaponId,
                usage.SourceEntityId > 0 ? usage.SourceEntityId : result.AttackerId.Value,
                usage.TargetEntityId > 0 ? usage.TargetEntityId : result.TargetId.Value,
                usage.HitZoneId,
                usage.BodyPartId,
                usage.LocalFrame,
                damageEvent.ProfileResult.IncomingDamage > 0
                    ? damageEvent.ProfileResult.IncomingDamage
                    : result.Damage,
                damageEvent.FinalDamage,
                result.Kind,
                0,
                0,
                hitPointWorld: result.HitPointWorld,
                hitNormalWorld: result.HitNormalWorld);
            return TryCreateFromTelemetry(row, entities, out textEvent);
        }

        public static bool TryCreateFromScatterImpact(
            ScatterImpactResolvedEvent evt,
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            out CharacterTestFloatingCombatTextEvent textEvent)
        {
            if (evt.ScatterImpact <= 0 || evt.TriggeredBreak)
            {
                textEvent = default;
                return false;
            }

            CharacterTestCharacterEntity target = FindEntityByGameplayId(entities, evt.EntityId);
            if (target == null)
            {
                textEvent = default;
                return false;
            }

            textEvent = new CharacterTestFloatingCombatTextEvent(
                evt.Frame,
                CharacterTestFloatingCombatTextKind.ScatterImpact,
                evt.SourceId,
                target.Id.Value,
                "SCATTER +" + evt.ScatterImpact,
                ResolveEntityAnchor(target),
                70,
                CharacterTestFloatingCombatTextStyle.ScatterImpact);
            return true;
        }

        public static bool TryCreateFromCohesionBreak(
            CohesionBreakEvent evt,
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            out CharacterTestFloatingCombatTextEvent textEvent)
        {
            CharacterTestCharacterEntity target = FindEntityByGameplayId(entities, evt.EntityId);
            if (target == null)
            {
                textEvent = default;
                return false;
            }

            textEvent = new CharacterTestFloatingCombatTextEvent(
                evt.Frame,
                CharacterTestFloatingCombatTextKind.CohesionBreak,
                evt.SourceId,
                target.Id.Value,
                "破聚!",
                ResolveEntityAnchor(target),
                140,
                CharacterTestFloatingCombatTextStyle.CohesionBreak);
            return true;
        }

        private static void AppendDamageEvents(
            IReadOnlyList<DamageTelemetryRow> rows,
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            IList<CharacterTestFloatingCombatTextEvent> output)
        {
            if (rows == null)
                return;

            for (int i = 0; i < rows.Count; i++)
            {
                if (TryCreateFromTelemetry(rows[i], entities, out CharacterTestFloatingCombatTextEvent evt))
                    output.Add(evt);
            }
        }

        private static void AppendHitPresentationEventsIfNeeded(
            IReadOnlyList<HitPresentationEvent> events,
            IReadOnlyList<DamageTelemetryRow> telemetryRows,
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            IList<CharacterTestFloatingCombatTextEvent> output)
        {
            if ((telemetryRows != null && telemetryRows.Count > 0) || events == null)
                return;

            for (int i = 0; i < events.Count; i++)
            {
                if (TryCreateFromHitPresentation(events[i], entities, out CharacterTestFloatingCombatTextEvent evt))
                    output.Add(evt);
            }
        }

        private static void AppendScatterImpactEvents(
            IReadOnlyList<ScatterImpactResolvedEvent> events,
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            IList<CharacterTestFloatingCombatTextEvent> output)
        {
            if (events == null)
                return;

            for (int i = 0; i < events.Count; i++)
            {
                if (TryCreateFromScatterImpact(events[i], entities, out CharacterTestFloatingCombatTextEvent evt))
                    output.Add(evt);
            }
        }

        private static void AppendCohesionBreakEvents(
            IReadOnlyList<CohesionBreakEvent> events,
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            IList<CharacterTestFloatingCombatTextEvent> output)
        {
            if (events == null)
                return;

            for (int i = 0; i < events.Count; i++)
            {
                if (TryCreateFromCohesionBreak(events[i], entities, out CharacterTestFloatingCombatTextEvent evt))
                    output.Add(evt);
            }
        }

        private static CharacterTestFloatingCombatTextEvent CreateInvalid(
            RuntimeFrame frame,
            int sourceEntityId,
            int targetEntityId,
            string text,
            Vector3 worldPosition)
        {
            return new CharacterTestFloatingCombatTextEvent(
                frame,
                CharacterTestFloatingCombatTextKind.Invalid,
                sourceEntityId,
                targetEntityId,
                text,
                worldPosition,
                40,
                CharacterTestFloatingCombatTextStyle.Invalid);
        }

        private static bool TryResolveWorldPosition(
            FixVector3 hitPoint,
            int targetEntityId,
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            out Vector3 worldPosition)
        {
            if (!hitPoint.IsZero)
            {
                worldPosition = ToUnityVector(hitPoint);
                return true;
            }

            CharacterTestCharacterEntity target = FindEntityByCharacterId(entities, targetEntityId);
            if (target != null)
            {
                worldPosition = ResolveEntityAnchor(target);
                return true;
            }

            worldPosition = default;
            return false;
        }

        private static Vector3 ResolveEntityAnchor(CharacterTestCharacterEntity entity)
        {
            CharacterTestCharacterTransformComponent transform = entity.Transform;
            return new Vector3(transform.X, transform.Y + TargetFallbackHeightMeters, transform.Z);
        }

        private static CharacterTestCharacterEntity FindEntityByCharacterId(
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            int entityId)
        {
            if (entities == null || entityId <= 0)
                return null;

            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity != null && entity.Id.Value == entityId)
                    return entity;
            }

            return null;
        }

        private static CharacterTestCharacterEntity FindEntityByGameplayId(
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            GameplayEntityId gameplayEntityId)
        {
            if (entities == null || !gameplayEntityId.IsValid)
                return null;

            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity != null && entity.GameplayEntityId.Equals(gameplayEntityId))
                    return entity;
            }

            return null;
        }

        private static Vector3 ToUnityVector(FixVector3 value)
        {
            return new Vector3(ToFloat(value.X), ToFloat(value.Y), ToFloat(value.Z));
        }

        private static float ToFloat(Fix64 value)
        {
            return value.RawValue / (float)Fix64.Scale;
        }
    }

}

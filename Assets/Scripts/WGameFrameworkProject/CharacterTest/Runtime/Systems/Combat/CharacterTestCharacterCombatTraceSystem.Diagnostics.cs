using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.Animation;
using MxFramework.CharacterAction;
using MxFramework.Combat.Animation;
using MxFramework.Combat.Core;
using MxFramework.Combat.Damage;
using MxFramework.Combat.GameplayBridge;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Physics;
using MxFramework.Combat.Projectiles;
using MxFramework.Combat.Shields;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using MxFramework.Vfx;
using MxFramework.Vfx.CombatBridge;
using Unity.Profiling;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestCharacterCombatTraceSystem
    {
        private DamageProfileResolveResult ResolveDamagePayload(HitResolveResult result)
        {
            return _damagePayloadsByHit.TryGetValue(
                new CharacterTestResolvedHitKey(result),
                out DamageProfileResolveResult payload)
                ? payload
                : default;
        }

        private CombatHitBuffApplicationConfig[] ResolveOnHitBuffs(HitResolveResult result)
        {
            return _onHitBuffsByHit.TryGetValue(
                new CharacterTestResolvedHitKey(result),
                out CombatHitBuffApplicationConfig[] onHitBuffs)
                ? onHitBuffs
                : Array.Empty<CombatHitBuffApplicationConfig>();
        }

        private void BuildWeaponHitAudioEvents(RuntimeFrame frame)
        {
            IReadOnlyList<DamageResolvedEvent> damageEvents = _damageApplier.DamageResolvedEvents;
            if (damageEvents == null || damageEvents.Count == 0)
                return;

            if (_weaponHitAudioEvents.Capacity < damageEvents.Count)
                _weaponHitAudioEvents.Capacity = damageEvents.Count;

            for (int i = 0; i < damageEvents.Count; i++)
            {
                DamageResolvedEvent damageEvent = damageEvents[i];
                HitResolveResult result = damageEvent.HitResult;
                if (!_weaponHitAudioByHit.TryGetValue(
                        new CharacterTestResolvedHitKey(result),
                        out CharacterTestWeaponHitAudioMetadata metadata))
                {
                    continue;
                }

                if (metadata.TryCreateEvent(
                        frame,
                        result,
                        damageEvent.FinalDamage,
                        ResolveWeaponHitAudioEffectiveImpact(result, metadata),
                        out CharacterTestWeaponHitAudioEvent audioEvent))
                {
                    _weaponHitAudioEvents.Add(audioEvent);
                }
            }
        }

        private float ResolveWeaponHitAudioEffectiveImpact(
            HitResolveResult result,
            CharacterTestWeaponHitAudioMetadata metadata)
        {
            CharacterTestResolvedHitKey hitKey = new CharacterTestResolvedHitKey(result);
            IReadOnlyList<DamageTelemetryRow> rows = _damageApplier.DamageTelemetryRows;
            if (rows == null || rows.Count == 0)
                return 0f;

            for (int i = 0; i < rows.Count; i++)
            {
                DamageTelemetryRow row = rows[i];
                if (row.ActionInstanceId == hitKey.ActionInstanceId
                    && row.RuntimeFrame == hitKey.Frame.Value
                    && row.ResultKind == result.Kind
                    && (row.StableEventId == metadata.Damage.Usage.StableEventId)
                    && row.SourceEntityId == CharacterTestCombatEntityLookup.ResolveCharacterEntityId(hitKey.AttackerId)
                    && row.TargetEntityId == CharacterTestCombatEntityLookup.ResolveCharacterEntityId(hitKey.TargetId))
                {
                    return row.EffectiveImpact;
                }
            }

            return 0f;
        }

        private void RecordHitPointDebugFrames(long runtimeFrame)
        {
            for (int i = 0; i < _results.Count; i++)
            {
                HitResolveResult result = _results[i];
                CharacterTestHitZoneResolution hitZone =
                    _hitZonesByHit.TryGetValue(new CharacterTestResolvedHitKey(result), out CharacterTestHitZoneResolution resolved)
                        ? resolved
                        : CharacterTestHitZoneResolution.Default;
                if (TryCreateHitPointDebugFrame(runtimeFrame, result, hitZone, out CharacterTestHitPointDebugFrame frame))
                    _hitPointDebugFrames.Add(frame);
            }
        }

        internal static bool TryCreateHitPointDebugFrame(
            long runtimeFrame,
            HitResolveResult result,
            CharacterTestHitZoneResolution hitZone,
            out CharacterTestHitPointDebugFrame frame)
        {
            frame = default;
            if (!CharacterTestHitPointDebugFrame.ShouldRecordKind(result.Kind))
                return false;

            frame = new CharacterTestHitPointDebugFrame(
                runtimeFrame,
                CharacterTestCombatEntityLookup.ResolveCharacterEntityId(result.AttackerId),
                CharacterTestCombatEntityLookup.ResolveCharacterEntityId(result.TargetId),
                result.Kind,
                result.TraceId,
                result.HitPointWorld,
                result.HitNormalWorld,
                hitZone.HitZoneId,
                hitZone.BodyPartId);
            return true;
        }

        private void ReportHitTraceError(int entityId, CharacterActionId actionId, int traceProfileId,
            string clipKey, int localFrame, string code, string message) =>
            ReportHitTraceError(entityId, actionId, traceProfileId.ToString(CultureInfo.InvariantCulture), clipKey, localFrame, code, message);

        private void ReportHitTraceError(int entityId, CharacterActionId actionId, int firstId,
            int secondId, int localFrame, string code, string message) =>
            ReportHitTraceError(entityId, actionId, firstId.ToString(CultureInfo.InvariantCulture), secondId.ToString(CultureInfo.InvariantCulture), localFrame, code, message);

        private void ReportHitTraceWarning(int entityId, CharacterActionId actionId, int traceProfileId,
            string clipKey, int localFrame, string code, string message) =>
            ReportHitTraceWarning(entityId, actionId, traceProfileId.ToString(CultureInfo.InvariantCulture), clipKey, localFrame, code, message);

        private void ReportHitTraceWarning(int entityId, CharacterActionId actionId, int firstId,
            int secondId, int localFrame, string code, string message) =>
            ReportHitTraceWarning(entityId, actionId, firstId.ToString(CultureInfo.InvariantCulture), secondId.ToString(CultureInfo.InvariantCulture), localFrame, code, message);

        private void ReportHitTraceError(
            int entityId,
            CharacterActionId actionId,
            string traceProfileId,
            string clipKey,
            int localFrame,
            string code,
            string message)
        {
            string key = code
                + "|"
                + entityId.ToString(CultureInfo.InvariantCulture)
                + "|"
                + actionId.Value.ToString(CultureInfo.InvariantCulture)
                + "|"
                + (traceProfileId ?? string.Empty)
                + "|"
                + (clipKey ?? string.Empty)
                + "|"
                + localFrame.ToString(CultureInfo.InvariantCulture);
            if (!_reportedErrors.Add(key))
                return;

            string formatted = code
                + " action=" + (actionId.IsValid ? actionId.ToString() : "none")
                + " trace=" + CharacterTestCombatTraceText.EmptyAsNone(traceProfileId)
                + " clip=" + CharacterTestCombatTraceText.EmptyAsNone(clipKey)
                + " entity=" + entityId.ToString(CultureInfo.InvariantCulture)
                + " localFrame=" + localFrame.ToString(CultureInfo.InvariantCulture)
                + " " + (message ?? string.Empty);
            _logger.Error(LogCategory, formatted);
            _traceRecorder?.AttackCombat(_frame, entityId, formatted, warning: true);
        }

        private void ReportMissingDirectHitPolicy(HitCandidate candidate)
        {
            ReportHitTraceError(
                CharacterTestCombatEntityLookup.ResolveCharacterEntityId(candidate.AttackerId),
                new CharacterActionId(Math.Max(0, candidate.ActionId)),
                string.Empty,
                string.Empty,
                candidate.Frame.Value,
                "DirectHitPolicyMissing",
                "Direct-hit candidate has no frozen HitEvent payload; candidate was rejected fail-closed.");
        }

        private void ReportHitTraceWarning(
            int entityId,
            CharacterActionId actionId,
            string traceProfileId,
            string clipKey,
            int localFrame,
            string code,
            string message)
        {
            string key = code
                + "|"
                + entityId.ToString(CultureInfo.InvariantCulture)
                + "|"
                + actionId.Value.ToString(CultureInfo.InvariantCulture)
                + "|"
                + (traceProfileId ?? string.Empty)
                + "|"
                + (clipKey ?? string.Empty)
                + "|"
                + localFrame.ToString(CultureInfo.InvariantCulture);
            if (!_reportedErrors.Add(key))
                return;

            string formatted = code
                + " action=" + CharacterTestCombatTraceText.EmptyAsNone(actionId)
                + " trace=" + CharacterTestCombatTraceText.EmptyAsNone(traceProfileId)
                + " clip=" + CharacterTestCombatTraceText.EmptyAsNone(clipKey)
                + " entity=" + entityId.ToString(CultureInfo.InvariantCulture)
                + " localFrame=" + localFrame.ToString(CultureInfo.InvariantCulture)
                + " " + (message ?? string.Empty);
            _logger.Warning(LogCategory, formatted);
            _traceRecorder?.AttackCombat(_frame, entityId, formatted, warning: true);
        }

        private void PruneAttackTraceDebugFrames(long currentFrame)
        {
            PruneAttackTraceDebugFrames(_attackTraceDebugFrames, currentFrame);
        }

        private void PruneHitPointDebugFrames(long currentFrame)
        {
            PruneHitPointDebugFrames(_hitPointDebugFrames, currentFrame);
        }

        internal static void PruneAttackTraceDebugFrames(
            List<CharacterTestAttackTraceDebugFrame> frames,
            long currentFrame)
        {
            if (frames == null || frames.Count == 0)
                return;

            long oldestFrame = currentFrame - AttackTraceDebugHistoryFrames;
            int removeCount = 0;
            while (removeCount < frames.Count && frames[removeCount].RuntimeFrame < oldestFrame)
                removeCount++;

            if (removeCount > 0)
                frames.RemoveRange(0, removeCount);
        }

        internal static void PruneHitPointDebugFrames(
            List<CharacterTestHitPointDebugFrame> frames,
            long currentFrame)
        {
            if (frames == null || frames.Count == 0)
                return;

            long oldestFrame = currentFrame - AttackTraceDebugHistoryFrames;
            int removeCount = 0;
            while (removeCount < frames.Count && frames[removeCount].RuntimeFrame < oldestFrame)
                removeCount++;

            if (removeCount > 0)
                frames.RemoveRange(0, removeCount);
        }

        private static bool TryResolveAreaRadius(int targetPolicyId, out Fix64 radius)
        {
            // Frozen authored policy parameters. These retain the previous radius
            // semantics, including area_2_5m's historical two-metre radius.
            switch (targetPolicyId)
            {
                case CharacterTestAuthoredContentIds.D406_Id1: radius = Fix64.FromInt(3); return true;
                case CharacterTestAuthoredContentIds.D406_Id2:
                case CharacterTestAuthoredContentIds.D406_Id8: radius = Fix64.FromInt(2); return true;
                case CharacterTestAuthoredContentIds.D406_Id3: radius = Fix64.FromInt(1); return true;
                default: radius = Fix64.Zero; return false;
            }
        }

        private static CombatFrame ToCombatFrame(RuntimeFrame frame)
        {
            return new CombatFrame((int)Math.Min(int.MaxValue, Math.Max(0, frame.Value)));
        }

        private static FixVector3 TransformLocal(CharacterTestCharacterTransformComponent transform, FixVector3 local)
        {
            FixVector3 rotated = RotateLocal(local, transform.YawDegrees);
            return new FixVector3(
                ToFix64(transform.X) + rotated.X,
                ToFix64(transform.Y) + rotated.Y,
                ToFix64(transform.Z) + rotated.Z);
        }

        private static FixVector3 RotateLocal(FixVector3 local, float yawDegrees)
        {
            double yaw = yawDegrees * Math.PI / 180.0;
            Fix64 sin = ToFix64(Math.Sin(yaw));
            Fix64 cos = ToFix64(Math.Cos(yaw));
            return new FixVector3(
                local.X * cos + local.Z * sin,
                local.Y,
                -local.X * sin + local.Z * cos);
        }

        private static FixVector3 ToFixVector3(MxAnimationBakedVector3 value)
        {
            return new FixVector3(
                Fix64.FromRaw(value.X * Fix64.Scale / 1000000L),
                Fix64.FromRaw(value.Y * Fix64.Scale / 1000000L),
                Fix64.FromRaw(value.Z * Fix64.Scale / 1000000L));
        }

        private static Fix64 ToFix64(double value)
        {
            return Fix64.FromRaw((long)Math.Round(value * Fix64.Scale));
        }

        internal static int StablePositiveId(long value)
        {
            if (value <= 0L)
                return 1;

            if (value <= int.MaxValue) return (int)value;
            uint folded = unchecked((uint)value ^ (uint)((ulong)value >> 32));
            int result = (int)(folded & 0x7fffffffU);
            return result == 0 ? 1 : result;
        }

        internal static int StablePositiveId(CharacterActionId value)
        {
            return value.IsValid ? value.Value : 1;
        }

        internal static int StablePolicyEventId(CharacterActionId actionId, int stableEventId)
        {
            if (!actionId.IsValid || stableEventId <= 0)
                throw new ArgumentOutOfRangeException(nameof(stableEventId), "An authored event ID is required.");
            return stableEventId;
        }

        private readonly struct ActiveHookPullState
        {
            public ActiveHookPullState(
                int casterEntityId,
                int targetEntityId,
                long startFrame,
                long endFrame,
                int actionInstanceId,
                int projectileId,
                int stableEventId,
                int stopDistanceMillimeters,
                int anchorForwardOffsetMillimeters,
                int accelerationMillimetersPerSecondSquared,
                int terminalSpeedMillimetersPerSecond)
            {
                CasterEntityId = casterEntityId < 0 ? 0 : casterEntityId;
                TargetEntityId = targetEntityId < 0 ? 0 : targetEntityId;
                StartFrame = startFrame < 0L ? 0L : startFrame;
                EndFrame = endFrame < 0L ? 0L : endFrame;
                ActionInstanceId = actionInstanceId < 0 ? 0 : actionInstanceId;
                ProjectileId = projectileId < 0 ? 0 : projectileId;
                StableEventId = stableEventId;
                StopDistanceMillimeters = stopDistanceMillimeters < 0 ? 0 : stopDistanceMillimeters;
                AnchorForwardOffsetMillimeters =
                    anchorForwardOffsetMillimeters < 0 ? 0 : anchorForwardOffsetMillimeters;
                AccelerationMillimetersPerSecondSquared =
                    accelerationMillimetersPerSecondSquared < 0 ? 0 : accelerationMillimetersPerSecondSquared;
                TerminalSpeedMillimetersPerSecond =
                    terminalSpeedMillimetersPerSecond < 0 ? 0 : terminalSpeedMillimetersPerSecond;
            }

            public int CasterEntityId { get; }
            public int TargetEntityId { get; }
            public long StartFrame { get; }
            public long EndFrame { get; }
            public int ActionInstanceId { get; }
            public int ProjectileId { get; }
            public int StableEventId { get; }
            public int StopDistanceMillimeters { get; }
            public int AnchorForwardOffsetMillimeters { get; }
            public int AccelerationMillimetersPerSecondSquared { get; }
            public int TerminalSpeedMillimetersPerSecond { get; }

            public static ActiveHookPullState FromSnapshot(CharacterTestHookPullSnapshot snapshot)
            {
                return new ActiveHookPullState(
                    snapshot.CasterEntityId,
                    snapshot.TargetEntityId,
                    snapshot.StartFrame,
                    snapshot.EndFrame,
                    snapshot.ActionInstanceId,
                    snapshot.ProjectileId,
                    snapshot.StableEventId,
                    snapshot.StopDistanceMillimeters,
                    snapshot.AnchorForwardOffsetMillimeters,
                    snapshot.AccelerationMillimetersPerSecondSquared,
                    snapshot.TerminalSpeedMillimetersPerSecond);
            }

            public CharacterTestHookPullSnapshot ToSnapshot()
            {
                return new CharacterTestHookPullSnapshot(
                    CasterEntityId,
                    TargetEntityId,
                    StartFrame,
                    EndFrame,
                    ActionInstanceId,
                    ProjectileId,
                    StableEventId,
                    StopDistanceMillimeters,
                    AnchorForwardOffsetMillimeters,
                    AccelerationMillimetersPerSecondSquared,
                    TerminalSpeedMillimetersPerSecond);
            }
        }

        private readonly struct ChannelBeamAimKey : IEquatable<ChannelBeamAimKey>
        {
            public ChannelBeamAimKey(int entityId, long actionInstanceId)
            {
                EntityId = entityId < 0 ? 0 : entityId;
                ActionInstanceId = actionInstanceId < 0L ? 0L : actionInstanceId;
            }

            public int EntityId { get; }

            public long ActionInstanceId { get; }

            public bool Equals(ChannelBeamAimKey other)
            {
                return EntityId == other.EntityId
                    && ActionInstanceId == other.ActionInstanceId;
            }

            public override bool Equals(object obj)
            {
                return obj is ChannelBeamAimKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (EntityId * 397) ^ ActionInstanceId.GetHashCode();
                }
            }
        }

        private readonly struct ChannelBeamAimState
        {
            public ChannelBeamAimState(FixVector3 direction, long createdFrame)
            {
                Direction = direction;
                CreatedFrame = createdFrame < 0L ? 0L : createdFrame;
            }

            public FixVector3 Direction { get; }

            public long CreatedFrame { get; }
        }

        private readonly struct MeteorBarrageCastKey : IEquatable<MeteorBarrageCastKey>
        {
            public MeteorBarrageCastKey(int entityId, long actionInstanceId)
            {
                EntityId = entityId < 0 ? 0 : entityId;
                ActionInstanceId = actionInstanceId < 0L ? 0L : actionInstanceId;
            }

            public int EntityId { get; }

            public long ActionInstanceId { get; }

            public bool Equals(MeteorBarrageCastKey other)
            {
                return EntityId == other.EntityId
                    && ActionInstanceId == other.ActionInstanceId;
            }

            public override bool Equals(object obj)
            {
                return obj is MeteorBarrageCastKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (EntityId * 397) ^ ActionInstanceId.GetHashCode();
                }
            }
        }

        private readonly struct MeteorBarrageTelegraphKey : IEquatable<MeteorBarrageTelegraphKey>
        {
            public MeteorBarrageTelegraphKey(int entityId, int actionInstanceId, int meteorIndex)
            {
                EntityId = entityId < 0 ? 0 : entityId;
                ActionInstanceId = actionInstanceId < 0 ? 0 : actionInstanceId;
                MeteorIndex = meteorIndex < 0 ? 0 : meteorIndex;
            }

            public int EntityId { get; }

            public int ActionInstanceId { get; }

            public int MeteorIndex { get; }

            public bool Equals(MeteorBarrageTelegraphKey other)
            {
                return EntityId == other.EntityId
                    && ActionInstanceId == other.ActionInstanceId
                    && MeteorIndex == other.MeteorIndex;
            }

            public override bool Equals(object obj)
            {
                return obj is MeteorBarrageTelegraphKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = EntityId;
                    hash = (hash * 397) ^ ActionInstanceId;
                    hash = (hash * 397) ^ MeteorIndex;
                    return hash;
                }
            }
        }

        private readonly struct MeteorBarrageCastState
        {
            public MeteorBarrageCastState(
                FixVector3 center,
                FixVector3 forward,
                FixVector3 right,
                long createdFrame)
            {
                Center = center;
                Forward = forward;
                Right = right;
                CreatedFrame = createdFrame < 0L ? 0L : createdFrame;
            }

            public FixVector3 Center { get; }

            public FixVector3 Forward { get; }

            public FixVector3 Right { get; }

            public long CreatedFrame { get; }

            public FixVector3 ResolveLandingPoint(int meteorIndex)
            {
                CharacterTestFireballProjectileAdapter.ResolveMeteorBarrageLocalOffset(
                    meteorIndex,
                    out Fix64 rightOffset,
                    out Fix64 forwardOffset);
                return Center + Right * rightOffset + Forward * forwardOffset;
            }
        }

        private readonly struct CharacterTestProjectileInteractionKey : IEquatable<CharacterTestProjectileInteractionKey>
        {
            public CharacterTestProjectileInteractionKey(
                long frame,
                int projectileId,
                int spawnSequence,
                int definitionStableId)
            {
                Frame = frame < 0L ? 0L : frame;
                ProjectileId = projectileId < 0 ? 0 : projectileId;
                SpawnSequence = spawnSequence < 0 ? 0 : spawnSequence;
                DefinitionStableId = definitionStableId;
            }

            public long Frame { get; }
            public int ProjectileId { get; }
            public int SpawnSequence { get; }
            public int DefinitionStableId { get; }

            public bool Equals(CharacterTestProjectileInteractionKey other)
            {
                return Frame == other.Frame
                    && ProjectileId == other.ProjectileId
                    && SpawnSequence == other.SpawnSequence
                    && (DefinitionStableId == other.DefinitionStableId);
            }

            public override bool Equals(object obj)
            {
                return obj is CharacterTestProjectileInteractionKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = Frame.GetHashCode();
                    hash = (hash * 397) ^ ProjectileId;
                    hash = (hash * 397) ^ SpawnSequence;
                    hash = (hash * 397) ^ DefinitionStableId;
                    return hash;
                }
            }
        }

        internal readonly struct CharacterTestBodyHitVolumeCacheKey : IEquatable<CharacterTestBodyHitVolumeCacheKey>
        {
            public CharacterTestBodyHitVolumeCacheKey(int entityId, int hitWindowId)
            {
                EntityId = entityId < 0 ? 0 : entityId;
                HitWindowId = hitWindowId < 0 ? 0 : hitWindowId;
            }

            public int EntityId { get; }

            public int HitWindowId { get; }

            public bool Equals(CharacterTestBodyHitVolumeCacheKey other)
            {
                return EntityId == other.EntityId
                    && HitWindowId == other.HitWindowId;
            }

            public override bool Equals(object obj)
            {
                return obj is CharacterTestBodyHitVolumeCacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (EntityId * 397) ^ HitWindowId;
                }
            }
        }

        private readonly struct TimelineProjectileStartKey : IEquatable<TimelineProjectileStartKey>
        {
            public TimelineProjectileStartKey(int projectileId, long frame, int timelineId)
            {
                ProjectileId = projectileId;
                Frame = frame;
                TimelineId = timelineId;
            }

            public int ProjectileId { get; }

            public long Frame { get; }

            public int TimelineId { get; }

            public bool Equals(TimelineProjectileStartKey other)
            {
                return ProjectileId == other.ProjectileId
                    && Frame == other.Frame
                    && TimelineId == other.TimelineId;
            }

            public override bool Equals(object obj)
            {
                return obj is TimelineProjectileStartKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = ProjectileId;
                    hash = (hash * 397) ^ Frame.GetHashCode();
                    hash = (hash * 397) ^ TimelineId;
                    return hash;
                }
            }
        }
    }
}



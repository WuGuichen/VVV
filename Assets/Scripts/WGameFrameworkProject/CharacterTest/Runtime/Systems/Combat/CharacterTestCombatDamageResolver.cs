using System;
using System.Collections.Generic;
using MxFramework.CharacterApplication;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Combat.Core;
using MxFramework.Combat.Damage;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Physics;
using MxFramework.Combat.Projectiles;
using MxFramework.Core.Math;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestDirectHitDamageEligibility
    {
        Eligible = 0,
        MissingPayload = 1,
        InvalidPayload = 2
    }

    internal sealed class CharacterTestCombatDamageResolver
    {
        private static readonly CombatProjectileImpactContext TornadoTickAreaPulseContext =
            new CombatProjectileImpactContext(CombatProjectileImpactSourceKind.AreaPulse);
        private const int CharacterTestSoftDefenseReference = 50;
        private const float CharacterTestSoftDefenseWeight = 0.6f;
        private static readonly int[] PoisonTickTags = { CharacterTestAuthoredContentIds.D410_Id47, CharacterTestAuthoredContentIds.D410_Id22 };
        internal static readonly CharacterActionDamagePayload PoisonTickDamage =
            new CharacterActionDamagePayload(
                CharacterTestAuthoredContentIds.D409_Id10,
                DamageFormulaIds.CharacterTestSoftDefenseV1,
                baseDamage: CharacterTestPoisonIds.PoisonedDamagePerTick,
                impactPower: 0,
                tags: PoisonTickTags,
                analysisCategory: CharacterTestAuthoredContentIds.D411_Id22,
                presentationProfileId: CharacterTestAuthoredContentIds.D407_Id18, presentationProfileLabel: CharacterTestCharacterActionConfigSource.PoisonTickPresentationProfileLabel);

        private readonly Action<int, CharacterActionId, string, string, int, string, string> _reportError;
        private readonly CharacterTestBodyPartDamageAuthorityBridge _bodyPartDamageBridge;
        private readonly Func<int, int> _sourceAttributeReader;
        private readonly Func<int, int> _targetAttributeReader;
        private readonly HashSet<CharacterTestDirectHitQueryKey> _blockedDirectHitQueries =
            new HashSet<CharacterTestDirectHitQueryKey>();
        private CharacterTestCombatEntityLookup _attributeLookup;
        private CombatEntityId _sourceAttributeEntityId;
        private CombatEntityId _targetAttributeEntityId;

        public CharacterTestCombatDamageResolver(
            Action<int, CharacterActionId, string, string, int, string, string> reportError,
            CharacterTestBodyPartDamageAuthorityBridge bodyPartDamageBridge = null)
        {
            _reportError = reportError;
            _bodyPartDamageBridge = bodyPartDamageBridge
                ?? new CharacterTestBodyPartDamageAuthorityBridge();
            _sourceAttributeReader = ReadSourceAttribute;
            _targetAttributeReader = ReadTargetAttribute;
        }

        public void ResolveCandidates(
            IReadOnlyList<HitCandidate> candidates,
            Dictionary<CharacterTestTracePayloadKey, CharacterTestTracePayload> payloadsByTrace,
            CharacterTestCombatEntityLookup lookup,
            List<HitCandidate> profiledCandidates,
            Dictionary<CharacterTestResolvedHitKey, DamageProfileResolveResult> damagePayloadsByHit,
            Dictionary<CharacterTestResolvedHitKey, CharacterTestHitZoneResolution> hitZonesByHit,
            Dictionary<CharacterTestResolvedHitKey, CombatHitBuffApplicationConfig[]> onHitBuffsByHit,
            Dictionary<CharacterTestResolvedHitKey, CharacterTestWeaponHitAudioMetadata> weaponHitAudioByHit,
            CombatPhysicsWorld physicsWorld,
            RuntimeFrame runtimeFrame,
            CharacterTestCombatWorldTargetClassificationSnapshot worldTargetClassifications,
            IReadOnlyDictionary<CombatQueryHeader, int> directHitPolicyEventIdsByQuery = null,
            Dictionary<CharacterTestResolvedHitKey, CharacterTestAccessoryHitEvidence> accessoryEvidenceByHit = null)
        {
            _bodyPartDamageBridge.BeginAuthorityFrame(runtimeFrame.Value);
            if (candidates == null || payloadsByTrace == null || lookup == null)
                return;

            _blockedDirectHitQueries.Clear();
            for (int i = 0; i < candidates.Count; i++)
            {
                HitCandidate candidate = candidates[i];
                var directHitQueryKey = new CharacterTestDirectHitQueryKey(candidate);
                if (_blockedDirectHitQueries.Contains(directHitQueryKey))
                    continue;
                var payloadKey = new CharacterTestTracePayloadKey(candidate.ActionInstanceId, candidate.TraceId);
                if (!payloadsByTrace.TryGetValue(payloadKey, out CharacterTestTracePayload payload))
                    continue;

                CharacterTestCharacterEntity source = lookup.FindEntity(payload.Trace.EntityId);
                bool environmentTarget = CharacterTestCombatHitEventResolver.IsStaticEnvironmentTarget(
                    candidate.TargetId);
                if (environmentTarget)
                {
                    CharacterTestCombatWorldQualification qualification =
                        CharacterTestCombatHitEventResolver.QualifyWorldTarget(
                            payload.Trace.HitEvent,
                            candidate.PhysicsHit,
                            runtimeFrame,
                            physicsWorld,
                            worldTargetClassifications,
                            out _);
                    if (qualification == CharacterTestCombatWorldQualification.StaleAuthority)
                    {
                        profiledCandidates.Clear();
                        damagePayloadsByHit?.Clear();
                        accessoryEvidenceByHit?.Clear();
                        hitZonesByHit?.Clear();
                        onHitBuffsByHit?.Clear();
                        weaponHitAudioByHit?.Clear();
                        return;
                    }
                    if (qualification == CharacterTestCombatWorldQualification.Rejected)
                        continue;

                    HitCandidate environmentCandidate = CreateEnvironmentBlockedCandidate(candidate, payload);
                    var environmentHitKey = new CharacterTestResolvedHitKey(environmentCandidate);
                    DamageProfileResolveResult environmentPayload = CreateEnvironmentBlockedPayload(payload, candidate);
                    damagePayloadsByHit[environmentHitKey] = environmentPayload;
                    accessoryEvidenceByHit?.Remove(environmentHitKey);
                    if (hitZonesByHit != null)
                        hitZonesByHit[environmentHitKey] = CharacterTestHitZoneResolution.Default;
                    if (weaponHitAudioByHit != null)
                    {
                        weaponHitAudioByHit[environmentHitKey] = new CharacterTestWeaponHitAudioMetadata(
                            environmentCandidate.PhysicsHit,
                            environmentPayload,
                            CharacterTestHitZoneResolution.Default,
                            ResolveWeaponHitAudioSurfaceMaterial(
                                physicsWorld,
                                environmentCandidate.PhysicsHit,
                                CombatSurfaceMaterial.Default));
                    }

                    profiledCandidates.Add(environmentCandidate);
                    _blockedDirectHitQueries.Add(directHitQueryKey);
                    continue;
                }

                if (!CharacterTestCombatHitEventResolver.AllowsTarget(
                        payload.Trace.HitEvent,
                        source,
                        candidate.TargetId,
                        lookup,
                        out _))
                {
                    continue;
                }

                CharacterTestBodyPartHitEvidence hitEvidence = ResolveHitZoneEvidence(
                    candidate.TargetId,
                    candidate.PhysicsHit.TargetColliderId,
                    lookup);
                CharacterTestHitZoneResolution hitZone = hitEvidence.HitZone;
                DamageProfileUsage usage = new DamageProfileUsage(
                    DamageProfileSourceKind.WeaponTrace,
                    payload.Trace.ActionId,
                    candidate.ActionInstanceId,
                    payload.Trace.HitEvent.StableEventId,
                    payload.Trace.EquipmentSlotId,
                    payload.Binding.WeaponId,
                    CharacterTestCombatEntityLookup.ResolveCharacterEntityId(candidate.AttackerId),
                    CharacterTestCombatEntityLookup.ResolveCharacterEntityId(candidate.TargetId),
                    hitZone.HitZoneId,
                    hitZone.BodyPartId,
                    candidate.Frame.Value,
                    payload.LocalFrame);
                if (!TryResolveCharacterActionDamage(
                        payload.Trace.DamagePayload,
                        usage,
                        ConfigureSourceAttributeReader(lookup, candidate.AttackerId),
                        ConfigureTargetAttributeReader(lookup, candidate.TargetId),
                        hitZone,
                        payload.Trace.DamagePayload.PresentationProfileId,
                        out DamageProfileResolveResult resolvedDamage))
                {
                    Report(
                        payload,
                        "HitTraceCommonDamageMissing",
                        "Missing hitEvent.damage common damage payload.");
                    continue;
                }

                if (!resolvedDamage.Success)
                {
                    Report(payload, "HitTraceCommonDamageInvalid", resolvedDamage.Reason);
                    continue;
                }

                CharacterTestCharacterEntity target = lookup.FindEntityByCombatId(candidate.TargetId);
                resolvedDamage = _bodyPartDamageBridge.ResolvePreHitDamage(
                    runtimeFrame.Value,
                    target?.BodyPartState,
                    hitEvidence,
                    CharacterTestBodyPartDamageSourcePolicy.DirectTrace,
                    resolvedDamage,
                    out CharacterTestBodyPartPreHitDecision preHitDecision);

                var profiledCandidate = new HitCandidate(
                    candidate.AttackerId,
                    candidate.TargetId,
                    candidate.ActionId,
                    candidate.ActionInstanceId,
                    candidate.TraceId,
                    candidate.Frame,
                    candidate.PhysicsHit,
                    resolvedDamage.IncomingDamage,
                    CharacterTestHitReactionResolver.CreateHitResolveReactionGate(resolvedDamage),
                    CharacterTestHitReactionResolver.CreateHitResolveKnockbackGate(resolvedDamage),
                    candidate.TargetState,
                    candidate.IncomingDirection,
                    candidate.ResolvePriority);
                var hitKey = new CharacterTestResolvedHitKey(profiledCandidate);
                int policyEventId = CharacterTestCharacterCombatTraceSystem.StablePolicyEventId(
                    payload.Trace.ActionId,
                    payload.Trace.HitEvent.StableEventId);
                if (directHitPolicyEventIdsByQuery != null
                    && directHitPolicyEventIdsByQuery.TryGetValue(
                        candidate.PhysicsHit.Query,
                        out int policyEventIdOverride)
                    && policyEventIdOverride > 0)
                {
                    policyEventId = policyEventIdOverride;
                }

                _bodyPartDamageBridge.RecordResolvedHit(
                    hitKey,
                    target?.BodyPartState,
                    hitEvidence,
                    CharacterTestBodyPartDamageSourcePolicy.DirectTrace,
                    usage.SourceEntityId,
                    usage.TargetEntityId,
                    policyEventId,
                    payload.LocalFrame,
                    preHitDecision);
                damagePayloadsByHit[hitKey] = resolvedDamage;
                if (accessoryEvidenceByHit != null)
                    accessoryEvidenceByHit[hitKey] = CharacterTestAccessoryHitEvidence.DirectTrace(payload.Trace.ActionId, candidate.IncomingDirection);
                if (hitZonesByHit != null)
                    hitZonesByHit[hitKey] = hitZone;
                if (onHitBuffsByHit != null && payload.Trace.HitEvent.OnHitBuffCount > 0)
                    onHitBuffsByHit[hitKey] = payload.Trace.HitEvent.OnHitBuffs;
                if (weaponHitAudioByHit != null)
                {
                    weaponHitAudioByHit[hitKey] = new CharacterTestWeaponHitAudioMetadata(
                        profiledCandidate.PhysicsHit,
                        resolvedDamage,
                        hitZone,
                        ResolveWeaponHitAudioSurfaceMaterial(
                            physicsWorld,
                            profiledCandidate.PhysicsHit,
                            CombatSurfaceMaterial.Default));
                }

                profiledCandidates.Add(profiledCandidate);
            }
        }

        private readonly struct CharacterTestDirectHitQueryKey : IEquatable<CharacterTestDirectHitQueryKey>
        {
            internal CharacterTestDirectHitQueryKey(HitCandidate candidate)
            {
                ActionInstanceId = candidate.ActionInstanceId;
                Frame = candidate.Frame;
                Query = candidate.PhysicsHit.Query;
            }

            private int ActionInstanceId { get; }
            private CombatFrame Frame { get; }
            private CombatQueryHeader Query { get; }

            public bool Equals(CharacterTestDirectHitQueryKey other)
            {
                return ActionInstanceId == other.ActionInstanceId
                    && Frame.Equals(other.Frame)
                    && Query.Equals(other.Query);
            }

            public override bool Equals(object obj)
            {
                return obj is CharacterTestDirectHitQueryKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = ActionInstanceId;
                    hash = (hash * 397) ^ Frame.GetHashCode();
                    hash = (hash * 397) ^ Query.GetHashCode();
                    return hash;
                }
            }
        }

        public bool TryResolveGameplayDamage(
            CharacterActionGameplayRequest request,
            CharacterTestCharacterEntity source,
            CombatEntityId targetId,
            CombatColliderId targetColliderId,
            CharacterTestCombatEntityLookup lookup,
            RuntimeFrame frame,
            DamageProfileSourceKind sourceKind,
            CharacterTestBodyPartDamageSourcePolicy bodyPartSourcePolicy,
            out DamageProfileResolveResult resolvedDamage,
            out CharacterTestHitZoneResolution hitZone)
        {
            resolvedDamage = default;
            hitZone = CharacterTestHitZoneResolution.Default;
            if (source == null || lookup == null)
                return false;

            CharacterTestBodyPartHitEvidence hitEvidence = ResolveHitZoneEvidence(
                targetId,
                targetColliderId,
                lookup);
            hitZone = hitEvidence.HitZone;
            int actionInstanceId = CharacterTestCharacterCombatTraceSystem.StablePositiveId(request.Metadata.InstanceId);
            DamageProfileUsage usage = new DamageProfileUsage(
                sourceKind,
                request.Metadata.ActionId,
                actionInstanceId,
                request.Metadata.StableEventId,
                0,
                0,
                source.Id.Value,
                CharacterTestCombatEntityLookup.ResolveCharacterEntityId(targetId),
                hitZone.HitZoneId,
                hitZone.BodyPartId,
                (int)Math.Min(int.MaxValue, Math.Max(0, frame.Value)),
                request.Metadata.LocalFrame);
            CombatEntityId sourceEntityId = new CombatEntityId(
                CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + source.Id.Value);
            if (!TryResolveCharacterActionDamage(
                    request.DamagePayload,
                    usage,
                    ConfigureSourceAttributeReader(lookup, sourceEntityId),
                    ConfigureTargetAttributeReader(lookup, targetId),
                    hitZone,
                    request.DamagePayload.PresentationProfileId,
                    out resolvedDamage))
            {
                ReportGameplay(
                    source,
                    request,
                    frame,
                    "GameplayCommonDamageMissing",
                    "Missing hitEvent.damage common damage payload.");
                return false;
            }

            if (resolvedDamage.Success)
            {
                CharacterTestCharacterEntity target = lookup.FindEntityByCombatId(targetId);
                resolvedDamage = _bodyPartDamageBridge.ResolvePreHitDamage(
                    frame.Value,
                    target?.BodyPartState,
                    hitEvidence,
                    bodyPartSourcePolicy,
                    resolvedDamage,
                    out CharacterTestBodyPartPreHitDecision preHitDecision);
                int traceId = CharacterTestCharacterCombatTraceSystem.StablePositiveId(
                    request.Metadata.StableEventId);
                var resolvedHit = new HitResolveResult(
                    sourceEntityId,
                    targetId,
                    CharacterTestCharacterCombatTraceSystem.StablePositiveId(request.Metadata.ActionId),
                    actionInstanceId,
                    traceId,
                    new CombatFrame((int)Math.Min(int.MaxValue, Math.Max(0, frame.Value))),
                    HitResolveKind.Damage,
                    resolvedDamage.IncomingDamage,
                    resolvedDamage.StaggerFrames,
                    resolvedDamage.Knockback);
                _bodyPartDamageBridge.RecordResolvedHit(
                    new CharacterTestResolvedHitKey(resolvedHit),
                    target?.BodyPartState,
                    hitEvidence,
                    bodyPartSourcePolicy,
                    usage.SourceEntityId,
                    usage.TargetEntityId,
                    CharacterTestCharacterCombatTraceSystem.StablePolicyEventId(
                        request.Metadata.ActionId,
                        request.Metadata.StableEventId),
                    request.Metadata.LocalFrame,
                    preHitDecision);
                return true;
            }

            ReportGameplay(source, request, frame, "GameplayCommonDamageInvalid", resolvedDamage.Reason);
            return false;
        }

        public bool TryResolveProjectileDamage(
            CombatProjectileSnapshot projectile,
            CombatProjectileImpactConfig impact,
            CombatProjectileImpactContext context,
            CharacterTestCharacterEntity source,
            CombatEntityId targetId,
            CombatColliderId targetColliderId,
            CharacterTestCombatEntityLookup lookup,
            RuntimeFrame frame,
            CharacterActionId actionId,
            out DamageProfileResolveResult resolvedDamage,
            out CharacterTestHitZoneResolution hitZone)
        {
            resolvedDamage = default;
            hitZone = CharacterTestHitZoneResolution.Default;
            if (source == null || lookup == null)
                return false;

            CombatHitEventConfig hitEvent = ResolveProjectileHitEvent(impact, context);
            if (!CharacterTestCombatHitEventResolver.AllowsTarget(
                    hitEvent,
                    source,
                    targetId,
                    lookup,
                    out bool environmentTarget))
            {
                return false;
            }

            if (environmentTarget)
            {
                resolvedDamage = CreateProjectileEnvironmentBlockedPayload(
                    projectile,
                    hitEvent,
                    source,
                    targetId,
                    frame,
                    actionId);
                return true;
            }

            int stableEventId = hitEvent.StableEventId;
            CharacterTestBodyPartHitEvidence hitEvidence = ResolveHitZoneEvidence(
                targetId,
                targetColliderId,
                lookup);
            CharacterTestBodyPartDamageSourcePolicy bodyPartSourcePolicy =
                ResolveProjectileBodyPartSourcePolicy(context);
            hitZone = hitEvidence.HitZone;
            int actionInstanceId = projectile.ActionInstanceId <= 0
                ? CharacterTestCharacterCombatTraceSystem.StablePositiveId(projectile.ProjectileId)
                : projectile.ActionInstanceId;
            DamageProfileUsage usage = new DamageProfileUsage(
                DamageProfileSourceKind.ProjectileImpact,
                actionId,
                actionInstanceId,
                stableEventId,
                0,
                0,
                source.Id.Value,
                CharacterTestCombatEntityLookup.ResolveCharacterEntityId(targetId),
                hitZone.HitZoneId,
                hitZone.BodyPartId,
                (int)Math.Min(int.MaxValue, Math.Max(0, frame.Value)),
                projectile.LocalFrame);
            if (!TryResolveProjectileDamagePayload(
                    hitEvent.Damage,
                    usage,
                    ConfigureSourceAttributeReader(lookup, projectile.SourceEntityId),
                    ConfigureTargetAttributeReader(lookup, targetId),
                    hitZone,
                    ResolveProjectilePresentationProfileId(stableEventId, impact.PresentationProfileId),
                    out resolvedDamage))
            {
                ReportProjectile(
                    source,
                    projectile,
                    impact,
                    frame,
                    actionId,
                    "ProjectileCommonDamageMissing",
                    "Missing impact.hitEvent.damage common damage payload.");
                return false;
            }

            if (resolvedDamage.Success)
            {
                CharacterTestCharacterEntity target = lookup.FindEntityByCombatId(targetId);
                resolvedDamage = _bodyPartDamageBridge.ResolvePreHitDamage(
                    frame.Value,
                    target?.BodyPartState,
                    hitEvidence,
                    bodyPartSourcePolicy,
                    resolvedDamage,
                    out CharacterTestBodyPartPreHitDecision preHitDecision);
                var resolvedHit = new HitResolveResult(
                    projectile.SourceEntityId,
                    targetId,
                    actionId.Value,
                    actionInstanceId,
                    projectile.TraceId,
                    new CombatFrame((int)Math.Min(int.MaxValue, Math.Max(0, frame.Value))),
                    HitResolveKind.Damage,
                    resolvedDamage.IncomingDamage,
                    resolvedDamage.StaggerFrames,
                    resolvedDamage.Knockback);
                _bodyPartDamageBridge.RecordResolvedHit(
                    new CharacterTestResolvedHitKey(resolvedHit),
                    target?.BodyPartState,
                    hitEvidence,
                    bodyPartSourcePolicy,
                    usage.SourceEntityId,
                    usage.TargetEntityId,
                    CharacterTestCharacterCombatTraceSystem.StablePolicyEventId(
                        actionId,
                        stableEventId),
                    projectile.LocalFrame,
                    preHitDecision);
                return true;
            }

            ReportProjectile(
                source,
                projectile,
                impact,
                frame,
                actionId,
                "ProjectileCommonDamageInvalid",
                resolvedDamage.Reason);
            return false;
        }

        public bool TryResolveProjectileDamage(
            CombatProjectileActiveView projectile,
            CombatProjectileImpactConfig impact,
            CombatProjectileImpactContext context,
            CharacterTestCharacterEntity source,
            CombatEntityId targetId,
            CombatColliderId targetColliderId,
            CharacterTestCombatEntityLookup lookup,
            RuntimeFrame frame,
            CharacterActionId actionId,
            out DamageProfileResolveResult resolvedDamage,
            out CharacterTestHitZoneResolution hitZone)
        {
            resolvedDamage = default;
            hitZone = CharacterTestHitZoneResolution.Default;
            if (source == null || lookup == null)
                return false;

            CombatHitEventConfig hitEvent = ResolveProjectileHitEvent(impact, context);
            if (!CharacterTestCombatHitEventResolver.AllowsTarget(
                    hitEvent,
                    source,
                    targetId,
                    lookup,
                    out bool environmentTarget))
            {
                return false;
            }

            if (environmentTarget)
            {
                resolvedDamage = CreateProjectileEnvironmentBlockedPayload(
                    projectile,
                    hitEvent,
                    source,
                    targetId,
                    frame,
                    actionId);
                return true;
            }

            int stableEventId = hitEvent.StableEventId;
            CharacterTestBodyPartHitEvidence hitEvidence = ResolveHitZoneEvidence(
                targetId,
                targetColliderId,
                lookup);
            CharacterTestBodyPartDamageSourcePolicy bodyPartSourcePolicy =
                ResolveProjectileBodyPartSourcePolicy(context);
            hitZone = hitEvidence.HitZone;
            int actionInstanceId = projectile.ActionInstanceId <= 0
                ? CharacterTestCharacterCombatTraceSystem.StablePositiveId(projectile.ProjectileId)
                : projectile.ActionInstanceId;
            DamageProfileUsage usage = new DamageProfileUsage(
                DamageProfileSourceKind.ProjectileImpact,
                actionId,
                actionInstanceId,
                stableEventId,
                0,
                0,
                source.Id.Value,
                CharacterTestCombatEntityLookup.ResolveCharacterEntityId(targetId),
                hitZone.HitZoneId,
                hitZone.BodyPartId,
                (int)Math.Min(int.MaxValue, Math.Max(0, frame.Value)),
                projectile.LocalFrame);
            if (!TryResolveProjectileDamagePayload(
                    hitEvent.Damage,
                    usage,
                    ConfigureSourceAttributeReader(lookup, projectile.SourceEntityId),
                    ConfigureTargetAttributeReader(lookup, targetId),
                    hitZone,
                    ResolveProjectilePresentationProfileId(stableEventId, impact.PresentationProfileId),
                    out resolvedDamage))
            {
                ReportProjectile(
                    source,
                    projectile,
                    impact,
                    frame,
                    actionId,
                    "ProjectileCommonDamageMissing",
                    "Missing impact.hitEvent.damage common damage payload.");
                return false;
            }

            if (resolvedDamage.Success)
            {
                CharacterTestCharacterEntity target = lookup.FindEntityByCombatId(targetId);
                resolvedDamage = _bodyPartDamageBridge.ResolvePreHitDamage(
                    frame.Value,
                    target?.BodyPartState,
                    hitEvidence,
                    bodyPartSourcePolicy,
                    resolvedDamage,
                    out CharacterTestBodyPartPreHitDecision preHitDecision);
                var resolvedHit = new HitResolveResult(
                    projectile.SourceEntityId,
                    targetId,
                    actionId.Value,
                    actionInstanceId,
                    projectile.TraceId,
                    new CombatFrame((int)Math.Min(int.MaxValue, Math.Max(0, frame.Value))),
                    HitResolveKind.Damage,
                    resolvedDamage.IncomingDamage,
                    resolvedDamage.StaggerFrames,
                    resolvedDamage.Knockback);
                _bodyPartDamageBridge.RecordResolvedHit(
                    new CharacterTestResolvedHitKey(resolvedHit),
                    target?.BodyPartState,
                    hitEvidence,
                    bodyPartSourcePolicy,
                    usage.SourceEntityId,
                    usage.TargetEntityId,
                    CharacterTestCharacterCombatTraceSystem.StablePolicyEventId(
                        actionId,
                        stableEventId),
                    projectile.LocalFrame,
                    preHitDecision);
                return true;
            }

            ReportProjectile(
                source,
                projectile,
                impact,
                frame,
                actionId,
                "ProjectileCommonDamageInvalid",
                resolvedDamage.Reason);
            return false;
        }

        internal bool TryResolveTornadoTickDamage(
            CombatProjectileActiveView projectile,
            CharacterTestCharacterEntity source,
            CombatEntityId targetId,
            CombatColliderId targetColliderId,
            CharacterTestCombatEntityLookup lookup,
            RuntimeFrame frame,
            out DamageProfileResolveResult resolvedDamage,
            out CharacterTestHitZoneResolution hitZone,
            out CombatHitEventConfig hitEvent)
        {
            resolvedDamage = default;
            hitZone = CharacterTestHitZoneResolution.Default;
            hitEvent = ResolveProjectileHitEvent(
                CharacterTestFireballProjectileAdapter.TornadoTickImpactConfig,
                TornadoTickAreaPulseContext);
            if (source == null || lookup == null)
                return false;

            if (!CharacterTestCombatHitEventResolver.AllowsTarget(
                    hitEvent,
                    source,
                    targetId,
                    lookup,
                    out bool environmentTarget))
            {
                return false;
            }

            if (environmentTarget)
            {
                resolvedDamage = CreateProjectileEnvironmentBlockedPayload(
                    projectile,
                    hitEvent,
                    source,
                    targetId,
                    frame,
                    CharacterTestCharacterActionConfigSource.TornadoActionId);
                return true;
            }

            int stableEventId = hitEvent.StableEventId;
            CharacterTestBodyPartHitEvidence hitEvidence = ResolveHitZoneEvidence(
                targetId,
                targetColliderId,
                lookup);
            hitZone = hitEvidence.HitZone;
            int actionInstanceId = projectile.ActionInstanceId <= 0
                ? CharacterTestCharacterCombatTraceSystem.StablePositiveId(projectile.ProjectileId)
                : projectile.ActionInstanceId;
            DamageProfileUsage usage = new DamageProfileUsage(
                DamageProfileSourceKind.ProjectileImpact,
                CharacterTestCharacterActionConfigSource.TornadoActionId,
                actionInstanceId,
                stableEventId,
                0,
                0,
                source.Id.Value,
                CharacterTestCombatEntityLookup.ResolveCharacterEntityId(targetId),
                hitZone.HitZoneId,
                hitZone.BodyPartId,
                (int)Math.Min(int.MaxValue, Math.Max(0, frame.Value)),
                projectile.LocalFrame);
            if (!TryResolveProjectileDamagePayload(
                    hitEvent.Damage,
                    usage,
                    ConfigureSourceAttributeReader(lookup, projectile.SourceEntityId),
                    ConfigureTargetAttributeReader(lookup, targetId),
                    hitZone,
                    ResolveProjectilePresentationProfileId(
                        stableEventId,
                        CharacterTestFireballProjectileAdapter.TornadoTickImpactConfig.PresentationProfileId),
                    out resolvedDamage))
            {
                ReportProjectile(
                    source,
                    projectile,
                    CharacterTestFireballProjectileAdapter.TornadoTickImpactConfig,
                    frame,
                    CharacterTestCharacterActionConfigSource.TornadoActionId,
                    "ProjectileCommonDamageMissing",
                    "Missing impact.hitEvent.damage common damage payload.");
                return false;
            }

            if (resolvedDamage.Success)
            {
                CharacterTestCharacterEntity target = lookup.FindEntityByCombatId(targetId);
                resolvedDamage = _bodyPartDamageBridge.ResolvePreHitDamage(
                    frame.Value,
                    target?.BodyPartState,
                    hitEvidence,
                    CharacterTestBodyPartDamageSourcePolicy.NoPartArea,
                    resolvedDamage,
                    out CharacterTestBodyPartPreHitDecision preHitDecision);
                int traceId = CharacterTestCharacterCombatTraceSystem.StablePositiveId(
                    CharacterTestCharacterActionConfigSource.TornadoProjectileTickStableEventId);
                var resolvedHit = new HitResolveResult(
                    projectile.SourceEntityId,
                    targetId,
                    CharacterTestCharacterCombatTraceSystem.StablePositiveId(
                        CharacterTestCharacterActionConfigSource.TornadoActionId),
                    actionInstanceId,
                    traceId,
                    new CombatFrame((int)Math.Min(int.MaxValue, Math.Max(0, frame.Value))),
                    HitResolveKind.Damage,
                    resolvedDamage.IncomingDamage,
                    resolvedDamage.StaggerFrames,
                    resolvedDamage.Knockback);
                _bodyPartDamageBridge.RecordResolvedHit(
                    new CharacterTestResolvedHitKey(resolvedHit),
                    target?.BodyPartState,
                    hitEvidence,
                    CharacterTestBodyPartDamageSourcePolicy.NoPartArea,
                    usage.SourceEntityId,
                    usage.TargetEntityId,
                    CharacterTestCharacterCombatTraceSystem.StablePolicyEventId(
                        CharacterTestCharacterActionConfigSource.TornadoActionId,
                        stableEventId),
                    projectile.LocalFrame,
                    preHitDecision);
                return true;
            }

            ReportProjectile(
                source,
                projectile,
                CharacterTestFireballProjectileAdapter.TornadoTickImpactConfig,
                frame,
                CharacterTestCharacterActionConfigSource.TornadoActionId,
                "ProjectileCommonDamageInvalid",
                resolvedDamage.Reason);
            return false;
        }

        public bool TryResolveStatusTickDamage(
            CombatEntityId targetId,
            CombatColliderId targetColliderId,
            CharacterTestCombatEntityLookup lookup,
            RuntimeFrame frame,
            int sourceEntityId,
            int actionInstanceId,
            int localFrame,
            out DamageProfileResolveResult resolvedDamage,
            out CharacterTestHitZoneResolution hitZone)
        {
            resolvedDamage = default;
            hitZone = CharacterTestHitZoneResolution.Default;
            if (lookup == null)
                return false;

            CharacterTestBodyPartHitEvidence hitEvidence = ResolveHitZoneEvidence(
                targetId,
                targetColliderId,
                lookup);
            hitZone = hitEvidence.HitZone;
            DamageProfileUsage usage = new DamageProfileUsage(
                DamageProfileSourceKind.StatusTick,
                CharacterActionId.None,
                actionInstanceId,
                CharacterTestAuthoredContentIds.D401_Id15,
                0,
                0,
                sourceEntityId,
                CharacterTestCombatEntityLookup.ResolveCharacterEntityId(targetId),
                hitZone.HitZoneId,
                hitZone.BodyPartId,
                (int)Math.Min(int.MaxValue, Math.Max(0, frame.Value)),
                localFrame);
            if (!TryResolveCharacterActionDamage(
                    PoisonTickDamage,
                    usage,
                    null,
                    ConfigureTargetAttributeReader(lookup, targetId),
                    hitZone,
                    CharacterTestCharacterActionConfigSource.PoisonTickPresentationProfileId,
                    out resolvedDamage))
            {
                ReportStatusTick(
                    sourceEntityId,
                    localFrame,
                    "StatusTickCommonDamageMissing",
                    "Missing poison tick common damage payload.");
                return false;
            }

            if (resolvedDamage.Success)
            {
                CharacterTestCharacterEntity target = lookup.FindEntityByCombatId(targetId);
                resolvedDamage = _bodyPartDamageBridge.ResolvePreHitDamage(
                    frame.Value,
                    target?.BodyPartState,
                    hitEvidence,
                    CharacterTestBodyPartDamageSourcePolicy.StatusTick,
                    resolvedDamage,
                    out CharacterTestBodyPartPreHitDecision preHitDecision);
                int traceId = CharacterTestCharacterCombatTraceSystem.StablePositiveId(
                    CharacterTestCharacterActionConfigSource.PoisonTickStableEventId);
                var resolvedHit = new HitResolveResult(
                    CombatEntityId.None,
                    targetId,
                    CharacterActionId.None.Value,
                    actionInstanceId,
                    traceId,
                    new CombatFrame((int)Math.Min(int.MaxValue, Math.Max(0, frame.Value))),
                    HitResolveKind.Damage,
                    resolvedDamage.IncomingDamage,
                    resolvedDamage.StaggerFrames,
                    resolvedDamage.Knockback);
                _bodyPartDamageBridge.RecordResolvedHit(
                    new CharacterTestResolvedHitKey(resolvedHit),
                    target?.BodyPartState,
                    hitEvidence,
                    CharacterTestBodyPartDamageSourcePolicy.StatusTick,
                    usage.SourceEntityId,
                    usage.TargetEntityId,
                    CharacterTestCharacterCombatTraceSystem.StablePolicyEventId(
                        CharacterActionId.None,
                        CharacterTestCharacterActionConfigSource.PoisonTickStableEventId),
                    localFrame,
                    preHitDecision);
                return true;
            }

            ReportStatusTick(sourceEntityId, localFrame, "StatusTickCommonDamageInvalid", resolvedDamage.Reason);
            return false;
        }

        private Func<int, int> ConfigureSourceAttributeReader(
            CharacterTestCombatEntityLookup lookup,
            CombatEntityId sourceEntityId)
        {
            _attributeLookup = lookup;
            _sourceAttributeEntityId = sourceEntityId;
            return _sourceAttributeReader;
        }

        private Func<int, int> ConfigureTargetAttributeReader(
            CharacterTestCombatEntityLookup lookup,
            CombatEntityId targetEntityId)
        {
            _attributeLookup = lookup;
            _targetAttributeEntityId = targetEntityId;
            return _targetAttributeReader;
        }

        private int ReadSourceAttribute(int attributeId)
        {
            return _attributeLookup != null
                ? _attributeLookup.ReadAttribute(_sourceAttributeEntityId, attributeId)
                : 0;
        }

        private int ReadTargetAttribute(int attributeId)
        {
            return _attributeLookup != null
                ? _attributeLookup.ReadAttribute(_targetAttributeEntityId, attributeId)
                : 0;
        }

        private static HitCandidate CreateEnvironmentBlockedCandidate(
            HitCandidate candidate,
            CharacterTestTracePayload payload)
        {
            return new HitCandidate(
                candidate.AttackerId,
                candidate.TargetId,
                candidate.ActionId,
                candidate.ActionInstanceId,
                candidate.TraceId,
                candidate.Frame,
                candidate.PhysicsHit,
                damage: 0,
                staggerFrames: 0,
                knockback: default,
                targetState: HitTargetStateFlags.Alive | HitTargetStateFlags.Blocking,
                incomingDirection: candidate.IncomingDirection,
                resolvePriority: candidate.ResolvePriority);
        }

        private static DamageProfileResolveResult CreateEnvironmentBlockedPayload(
            CharacterTestTracePayload payload,
            HitCandidate candidate)
        {
            CombatHitEventConfig hitEvent = payload.Trace.HitEvent;
            var usage = new DamageProfileUsage(
                DamageProfileSourceKind.WeaponTrace,
                payload.Trace.ActionId,
                candidate.ActionInstanceId,
                hitEvent.StableEventId,
                payload.Trace.EquipmentSlotId,
                payload.Binding.WeaponId,
                CharacterTestCombatEntityLookup.ResolveCharacterEntityId(candidate.AttackerId),
                0,
                0,
                0,
                candidate.Frame.Value,
                payload.LocalFrame);
            return new DamageProfileResolveResult(
                true,
                "EnvironmentBlocked",
                0,
                0,
                0,
                0,
                0,
                0,
                default,
                0,
                0,
                0,
                usage);
        }

        private static DamageProfileResolveResult CreateProjectileEnvironmentBlockedPayload(
            CombatProjectileSnapshot projectile,
            CombatHitEventConfig hitEvent,
            CharacterTestCharacterEntity source,
            CombatEntityId targetId,
            RuntimeFrame frame,
            CharacterActionId actionId)
        {
            int actionInstanceId = projectile.ActionInstanceId <= 0
                ? CharacterTestCharacterCombatTraceSystem.StablePositiveId(projectile.ProjectileId)
                : projectile.ActionInstanceId;
            int stableEventId = hitEvent.StableEventId;
            var usage = new DamageProfileUsage(
                DamageProfileSourceKind.ProjectileImpact,
                actionId,
                actionInstanceId,
                stableEventId,
                0,
                0,
                source.Id.Value,
                0,
                0,
                0,
                (int)Math.Min(int.MaxValue, Math.Max(0, frame.Value)),
                projectile.LocalFrame);
            return new DamageProfileResolveResult(
                true,
                "EnvironmentBlocked",
                0,
                0,
                0,
                0,
                0,
                0,
                default,
                0,
                0,
                0,
                usage);
        }

        private static DamageProfileResolveResult CreateProjectileEnvironmentBlockedPayload(
            CombatProjectileActiveView projectile,
            CombatHitEventConfig hitEvent,
            CharacterTestCharacterEntity source,
            CombatEntityId targetId,
            RuntimeFrame frame,
            CharacterActionId actionId)
        {
            int actionInstanceId = projectile.ActionInstanceId <= 0
                ? CharacterTestCharacterCombatTraceSystem.StablePositiveId(projectile.ProjectileId)
                : projectile.ActionInstanceId;
            int stableEventId = hitEvent.StableEventId;
            var usage = new DamageProfileUsage(
                DamageProfileSourceKind.ProjectileImpact,
                actionId,
                actionInstanceId,
                stableEventId,
                0,
                0,
                source.Id.Value,
                0,
                0,
                0,
                (int)Math.Min(int.MaxValue, Math.Max(0, frame.Value)),
                projectile.LocalFrame);
            return new DamageProfileResolveResult(
                true,
                "EnvironmentBlocked",
                0,
                0,
                0,
                0,
                0,
                0,
                default,
                0,
                0,
                0,
                usage);
        }

        internal static CombatHitEventConfig ResolveProjectileHitEvent(
            CombatProjectileImpactConfig impact,
            CombatProjectileImpactContext context)
        {
            return context.IsAreaPulse ? impact.AreaHitEvent : impact.HitEvent;
        }

        internal static CharacterTestBodyPartDamageSourcePolicy ResolveProjectileBodyPartSourcePolicy(
            CombatProjectileImpactContext context)
        {
            return context.IsAreaPulse
                ? CharacterTestBodyPartDamageSourcePolicy.NoPartArea
                : CharacterTestBodyPartDamageSourcePolicy.Projectile;
        }

        /// <summary>
        /// Side-effect-free PreviewTrace gate backed by the same CharacterAction
        /// damage resolver used by actual direct-hit candidate profiling.
        /// Attribute readers are intentionally omitted: M1 needs eligibility,
        /// not a speculative final damage number.
        /// </summary>
        internal static CharacterTestDirectHitDamageEligibility EvaluateDirectHitDamageEligibility(
            CharacterActionDamagePayload damage,
            CharacterTestHitZoneResolution hitZone)
        {
            if (!TryResolveCharacterActionDamage(
                    damage,
                    default(DamageProfileUsage),
                    null,
                    null,
                    hitZone,
                    damage.PresentationProfileId,
                    out DamageProfileResolveResult resolvedDamage))
            {
                return CharacterTestDirectHitDamageEligibility.MissingPayload;
            }

            return resolvedDamage.Success
                ? CharacterTestDirectHitDamageEligibility.Eligible
                : CharacterTestDirectHitDamageEligibility.InvalidPayload;
        }

        /// <summary>
        /// Rehydrates the actual resolver's M1-compatible HitZone contract from
        /// frozen hurtbox metadata. Missing/invalid metadata retains the actual
        /// compatibility fallback instead of introducing M2 strict Zone rules.
        /// </summary>
        internal static CharacterTestHitZoneResolution ResolveFrozenHitZone(
            CharacterTestDamageGeometryColliderMetadata metadata)
        {
            if (metadata.HitZoneId <= 0
                || metadata.PartId <= 0)
            {
                return CharacterTestHitZoneResolution.Default;
            }

            return new CharacterTestHitZoneResolution(
                metadata.HitZoneId,
                metadata.PartId,
                CharacterTestReactionGroupIds.ResolveHeightGroup(
                    metadata.HitZoneId,
                    metadata.PartId,
                    0),
                1f,
                1f,
                1f,
                1f);
        }

        internal static bool TryResolveCharacterActionDamage(
            CharacterActionDamagePayload damage,
            DamageProfileUsage usage,
            Func<int, int> sourceAttributeReader,
            Func<int, int> targetAttributeReader,
            CharacterTestHitZoneResolution hitZone,
            int presentationProfileId,
            out DamageProfileResolveResult resolvedDamage)
        {
            if (!damage.HasInlineData)
            {
                resolvedDamage = default;
                return false;
            }

            resolvedDamage = ResolveCommonDamage(
                damage.DamageTypeId,
                damage.BaseDamage,
                damage.SourceAttributeScales,
                damage.ImpactPower,
                damage.LaunchPower,
                damage.Tags,
                damage.AnalysisCategory,
                presentationProfileId <= 0
                    ? damage.PresentationProfileId
                    : presentationProfileId,
                usage,
                sourceAttributeReader,
                targetAttributeReader,
                hitZone);
            return true;
        }

        internal static bool TryResolveProjectileDamagePayload(
            CombatHitDamageConfig damage,
            DamageProfileUsage usage,
            Func<int, int> sourceAttributeReader,
            Func<int, int> targetAttributeReader,
            CharacterTestHitZoneResolution hitZone,
            int presentationProfileId,
            out DamageProfileResolveResult resolvedDamage)
        {
            if (!damage.HasDamage)
            {
                resolvedDamage = default;
                return false;
            }

            resolvedDamage = ResolveCommonDamage(
                damage.DamageTypeId,
                damage.BaseDamage,
                damage.SourceAttributeScales,
                damage.ImpactPower,
                damage.LaunchPower,
                damage.Tags,
                damage.AnalysisCategory,
                presentationProfileId,
                usage,
                sourceAttributeReader,
                targetAttributeReader,
                hitZone);
            return true;
        }

        private static DamageProfileResolveResult ResolveCommonDamage(
            int damageTypeId,
            int baseDamage,
            DamageAttributeScale[] sourceAttributeScales,
            int impactPower,
            int launchPower,
            int[] tags,
            int analysisCategory,
            int presentationProfileId,
            DamageProfileUsage usage,
            Func<int, int> sourceAttributeReader,
            Func<int, int> targetAttributeReader,
            CharacterTestHitZoneResolution hitZone)
        {
            const int formulaId = DamageFormulaIds.CharacterTestSoftDefenseV1;
            DamageFormulaTrace trace = ResolveSoftDefenseTrace(
                formulaId,
                baseDamage,
                sourceAttributeScales,
                sourceAttributeReader,
                targetAttributeReader,
                hitZone.DamageMultiplier);
            int scaledImpactPower = Round(impactPower * ClampNonNegative(hitZone.ImpactPowerScale));
            return new DamageProfileResolveResult(
                true,
                string.Empty,
                0,
                damageTypeId,
                formulaId,
                trace.IncomingDamage,
                0,
                0,
                FixVector3.Zero,
                scaledImpactPower,
                0,
                presentationProfileId,
                usage,
                trace,
                tags,
                launchPower,
                analysisCategory);
        }

        private static DamageFormulaTrace ResolveSoftDefenseTrace(
            int formulaId,
            int baseDamage,
            DamageAttributeScale[] sourceAttributeScales,
            Func<int, int> sourceAttributeReader,
            Func<int, int> targetAttributeReader,
            float externalDamageMultiplier)
        {
            float sourceContribution = Sum(sourceAttributeScales, sourceAttributeReader);
            float offense = Math.Max(0f, Math.Max(0, baseDamage) + sourceContribution);
            float targetDefense = Math.Max(0f, targetAttributeReader?.Invoke(DamageFormulaIds.CharacterTestDefenseAttributeId) ?? 0f);
            float mitigation = targetDefense <= 0f
                ? 0f
                : CharacterTestSoftDefenseWeight * targetDefense / (targetDefense + CharacterTestSoftDefenseReference);
            mitigation = Clamp01(mitigation);
            float externalMultiplier = ClampNonNegative(externalDamageMultiplier);
            float rawDamage = offense * (1f - mitigation) * externalMultiplier;
            int incomingDamage = NonNegativeDamage(Round(rawDamage));
            return new DamageFormulaTrace(
                formulaId,
                baseDamage,
                SourceAttributeValue(sourceAttributeScales, sourceAttributeReader),
                SourceAttributeScale(sourceAttributeScales),
                sourceContribution,
                offense,
                targetDefense,
                CharacterTestSoftDefenseReference,
                CharacterTestSoftDefenseWeight,
                mitigation,
                1f,
                externalMultiplier,
                rawDamage,
                incomingDamage);
        }

        private static float Sum(DamageAttributeScale[] scales, Func<int, int> reader)
        {
            if (scales == null || scales.Length == 0 || reader == null)
                return 0f;

            float total = 0f;
            for (int i = 0; i < scales.Length; i++)
            {
                DamageAttributeScale scale = scales[i];
                if (scale.AttributeId <= 0 || scale.Scale.Equals(0f))
                    continue;
                total += reader(scale.AttributeId) * scale.Scale;
            }

            return total;
        }

        private static float SourceAttributeValue(DamageAttributeScale[] scales, Func<int, int> reader)
        {
            if (scales == null || scales.Length == 0 || reader == null)
                return 0f;

            for (int i = 0; i < scales.Length; i++)
            {
                DamageAttributeScale scale = scales[i];
                if (scale.AttributeId > 0 && !scale.Scale.Equals(0f))
                    return reader(scale.AttributeId);
            }

            return 0f;
        }

        private static float SourceAttributeScale(DamageAttributeScale[] scales)
        {
            if (scales == null || scales.Length == 0)
                return 0f;

            for (int i = 0; i < scales.Length; i++)
            {
                DamageAttributeScale scale = scales[i];
                if (scale.AttributeId > 0 && !scale.Scale.Equals(0f))
                    return scale.Scale;
            }

            return 0f;
        }

        private static int NonNegativeDamage(int value)
        {
            return value < 0 ? 0 : value;
        }

        private static int Round(float value)
        {
            return (int)Math.Round(value, MidpointRounding.AwayFromZero);
        }

        private static float ClampNonNegative(float value)
        {
            return value < 0f ? 0f : value;
        }

        private static float Clamp01(float value)
        {
            if (value < 0f)
                return 0f;
            return value > 1f ? 1f : value;
        }

        private static int ResolveProjectilePresentationProfileId(int stableEventId, int legacyPresentationProfileId)
        {
            if ((stableEventId == CharacterTestCharacterActionConfigSource.FireballProjectileImpactStableEventId)
                || (stableEventId == CharacterTestCharacterActionConfigSource.FireballSurgeProjectileImpactStableEventId)
                || (stableEventId == CharacterTestCharacterActionConfigSource.FireballReturnProjectileImpactStableEventId)
                || (stableEventId == CharacterTestCharacterActionConfigSource.FireballSwayProjectileImpactStableEventId)
                || (stableEventId == CharacterTestCharacterActionConfigSource.FireballHomingProjectileImpactStableEventId)
                || (stableEventId == CharacterTestCharacterActionConfigSource.FireballBezierProjectileImpactStableEventId))
                return CharacterTestCharacterActionConfigSource.FireballPresentationProfileId;
            if ((stableEventId == CharacterTestCharacterActionConfigSource.FireballBurstProjectileImpactStableEventId)
                || (stableEventId == CharacterTestCharacterActionConfigSource.FireballLobProjectileImpactStableEventId))
            {
                return CharacterTestCharacterActionConfigSource.FireballBurstPresentationProfileId;
            }
            if ((stableEventId == CharacterTestCharacterActionConfigSource.FlameLanceProjectileImpactStableEventId))
                return CharacterTestCharacterActionConfigSource.FlameLancePresentationProfileId;
            if ((stableEventId == CharacterTestCharacterActionConfigSource.TornadoProjectileImpactStableEventId))
                return CharacterTestCharacterActionConfigSource.TornadoPresentationProfileId;
            if ((stableEventId == CharacterTestCharacterActionConfigSource.TornadoProjectileTickStableEventId))
                return CharacterTestCharacterActionConfigSource.TornadoTickPresentationProfileId;
            if ((stableEventId == CharacterTestCharacterActionConfigSource.BeamLanceProjectileImpactStableEventId))
                return CharacterTestCharacterActionConfigSource.BeamLancePresentationProfileId;
            if ((stableEventId == CharacterTestCharacterActionConfigSource.LaserLanceProjectileImpactStableEventId))
                return CharacterTestCharacterActionConfigSource.LaserLancePresentationProfileId;
            if ((stableEventId == CharacterTestCharacterActionConfigSource.ChannelBeamProjectileImpactStableEventId))
                return CharacterTestCharacterActionConfigSource.ChannelBeamPresentationProfileId;
            if ((stableEventId == CharacterTestCharacterActionConfigSource.ChainLightningImpactStableEventId))
                return CharacterTestCharacterActionConfigSource.ChainLightningPresentationProfileId;
            if ((stableEventId == CharacterTestCharacterActionConfigSource.PoisonBottleProjectileImpactStableEventId))
                return CharacterTestCharacterActionConfigSource.PoisonBottlePresentationProfileId;
            if ((stableEventId == CharacterTestCharacterActionConfigSource.StunBoltProjectileImpactStableEventId))
                return CharacterTestCharacterActionConfigSource.StunBoltPresentationProfileId;
            if ((stableEventId == CharacterTestCharacterActionConfigSource.MeteorBarrageProjectileImpactStableEventId))
                return CharacterTestCharacterActionConfigSource.MeteorBarragePresentationProfileId;
            if ((stableEventId == CharacterTestCharacterActionConfigSource.SwordWaveProjectileImpactStableEventId))
                return CharacterTestCharacterActionConfigSource.SwordWavePresentationProfileId;
            if ((stableEventId == CharacterTestCharacterActionConfigSource.HookChainProjectileImpactStableEventId))
                return CharacterTestCharacterActionConfigSource.HookChainPresentationProfileId;
            if ((stableEventId == CharacterTestCharacterActionConfigSource.ArcMineTriggerStableEventId))
                return CharacterTestCharacterActionConfigSource.ArcMinePresentationProfileId;
            if ((stableEventId == CharacterTestCharacterActionConfigSource.RuneTurretShotProjectileImpactStableEventId))
                return CharacterTestCharacterActionConfigSource.RuneTurretPresentationProfileId;

            return legacyPresentationProfileId;
        }

        private void Report(CharacterTestTracePayload payload, string code, string message)
        {
            _reportError?.Invoke(
                payload.Trace.EntityId,
                payload.Trace.ActionId,
                payload.Trace.DescribeSelector(),
                payload.ClipKey,
                payload.LocalFrame,
                code,
                message);
        }

        private void ReportGameplay(
            CharacterTestCharacterEntity source,
            CharacterActionGameplayRequest request,
            RuntimeFrame frame,
            string code,
            string message)
        {
            _reportError?.Invoke(
                source != null ? source.Id.Value : 0,
                request.Metadata.ActionId,
                request.TargetPolicyId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                string.Empty,
                request.Metadata.LocalFrame,
                code,
                message);
        }

        private void ReportProjectile(
            CharacterTestCharacterEntity source,
            CombatProjectileSnapshot projectile,
            CombatProjectileImpactConfig impact,
            RuntimeFrame frame,
            CharacterActionId actionId,
            string code,
            string message)
        {
            _reportError?.Invoke(
                source != null ? source.Id.Value : 0,
                actionId,
                impact.StableEventId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                string.Empty,
                projectile.LocalFrame,
                code,
                message);
        }

        private void ReportProjectile(
            CharacterTestCharacterEntity source,
            CombatProjectileActiveView projectile,
            CombatProjectileImpactConfig impact,
            RuntimeFrame frame,
            CharacterActionId actionId,
            string code,
            string message)
        {
            _reportError?.Invoke(
                source != null ? source.Id.Value : 0,
                actionId,
                impact.StableEventId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                string.Empty,
                projectile.LocalFrame,
                code,
                message);
        }

        private void ReportStatusTick(
            int sourceEntityId,
            int localFrame,
            string code,
            string message)
        {
            _reportError?.Invoke(
                sourceEntityId,
                CharacterActionId.None,
                CharacterTestCharacterActionConfigSource.PoisonTickStableEventId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                string.Empty,
                localFrame,
                code,
                message);
        }

        internal static CharacterTestHitZoneResolution ResolveHitZone(
            CombatEntityId targetId,
            CombatColliderId colliderId,
            CharacterTestCombatEntityLookup lookup)
        {
            return ResolveHitZoneEvidence(targetId, colliderId, lookup).HitZone;
        }

        internal static CharacterTestBodyPartHitEvidence ResolveHitZoneEvidence(
            CombatEntityId targetId,
            CombatColliderId colliderId,
            CharacterTestCombatEntityLookup lookup)
        {
            int colliderIndex = colliderId.Value - CharacterTestCharacterPhysicsRuntime.CharacterHurtboxColliderIdStart;
            if (colliderIndex < 0)
            {
                return new CharacterTestBodyPartHitEvidence(
                    CharacterTestHitZoneResolution.Default,
                    CharacterTestBodyPartHitProvenance.DefaultFallback);
            }

            CharacterTestCharacterEntity target = lookup?.FindEntityByCombatId(targetId);
            CharacterImportedPackage package = CharacterTestGeneratedPackageSource.LoadPackageForEntity(target);
            if (package == null)
            {
                return new CharacterTestBodyPartHitEvidence(
                    CharacterTestHitZoneResolution.Default,
                    CharacterTestBodyPartHitProvenance.DefaultFallback);
            }
            CharacterBodyColliderRuntimeBinding[] colliders = package.Geometry?.BodyColliders ?? Array.Empty<CharacterBodyColliderRuntimeBinding>();
            if (colliders.Length > 0 && (uint)colliderIndex >= (uint)colliders.Length)
            {
                return new CharacterTestBodyPartHitEvidence(
                    CharacterTestHitZoneResolution.Default,
                    CharacterTestBodyPartHitProvenance.DefaultFallback);
            }

            CharacterBodyColliderRuntimeBinding collider = colliders.Length == 0
                ? null
                : colliders[colliderIndex];
            if (colliders.Length > 0 && collider == null)
            {
                return new CharacterTestBodyPartHitEvidence(
                    CharacterTestHitZoneResolution.Default,
                    CharacterTestBodyPartHitProvenance.InvalidEvidence);
            }

            CharacterImportedConfigSet configs = package?.Configs;
            CharacterConfig character = ResolveDefaultCharacter(configs);
            CharacterBodyProfileConfig bodyProfile = character == null
                ? null
                : configs?.FindBodyProfile(character.BodyProfileId);
            BodyPartHitZoneResolveResult result = BodyPartHitZoneResolver.Resolve(
                bodyProfile,
                configs?.BodyParts ?? Array.Empty<CharacterBodyPartConfig>(),
                collider?.HitZoneId ?? CharacterTestHitZoneDefaults.HitZoneId);
            if (!result.IsSuccess)
            {
                return new CharacterTestBodyPartHitEvidence(
                    CharacterTestHitZoneResolution.Default,
                    CharacterTestBodyPartHitProvenance.DefaultFallback);
            }

            float damageMultiplier = collider != null && collider.DamageMultiplierOverride > 0f
                ? collider.DamageMultiplierOverride
                : result.DamageMultiplier;
            float impactPowerScale = collider != null && collider.ImpactPowerScaleOverride > 0f
                ? collider.ImpactPowerScaleOverride
                : result.ImpactPowerScale;

            var hitZone = new CharacterTestHitZoneResolution(
                result.HitZoneId,
                (result.PartId == 0) ? collider?.PartId ?? 0 : result.PartId,
                CharacterTestReactionGroupIds.ResolveHeightGroup(result.HitZoneId, result.PartId, result.ReactionGroupId),
                damageMultiplier,
                result.StaggerScale,
                result.ImpulseScale,
                impactPowerScale);
            CharacterTestBodyPartHitProvenance provenance;
            if (collider == null
                || (collider.PartId == 0)
                || (collider.HitZoneId == 0)
                || (result.PartId == 0))
            {
                provenance = CharacterTestBodyPartHitProvenance.NoPartEvidence;
            }
            else if (!(collider.PartId == result.PartId)
                || !(collider.HitZoneId == result.HitZoneId))
            {
                provenance = CharacterTestBodyPartHitProvenance.InvalidEvidence;
            }
            else
            {
                provenance = CharacterTestBodyPartHitProvenance.ExplicitColliderBinding;
            }

            return new CharacterTestBodyPartHitEvidence(hitZone, provenance);
        }

        internal static CombatSurfaceMaterial ResolveWeaponHitAudioSurfaceMaterial(
            CombatPhysicsWorld world,
            in CombatQueryResult hit,
            CombatSurfaceMaterial fallback)
        {
            if (world == null)
                return fallback;

            switch (hit.TargetShapeKind)
            {
                case CombatPhysicsShapeKind.Sphere:
                    if (world.TryGetSphereCollider(
                        hit.TargetBodyId,
                        hit.TargetColliderId,
                        out CombatPhysicsSphereCollider sphereCollider))
                    {
                        return sphereCollider.SurfaceMaterial;
                    }

                    break;
                case CombatPhysicsShapeKind.Capsule:
                    if (world.TryGetCapsuleCollider(
                        hit.TargetBodyId,
                        hit.TargetColliderId,
                        out CombatPhysicsCapsuleCollider capsuleCollider))
                    {
                        return capsuleCollider.SurfaceMaterial;
                    }

                    break;
                case CombatPhysicsShapeKind.Obb:
                    if (world.TryGetObbCollider(
                        hit.TargetBodyId,
                        hit.TargetColliderId,
                        out CombatPhysicsObbCollider obbCollider))
                    {
                        return obbCollider.SurfaceMaterial;
                    }

                    break;
                default:
                    if (world.TryGetAabbCollider(
                        hit.TargetBodyId,
                        hit.TargetColliderId,
                        out CombatPhysicsAabbCollider aabbCollider))
                    {
                        return aabbCollider.SurfaceMaterial;
                    }

                    break;
            }

            return fallback;
        }

        private static CharacterConfig ResolveDefaultCharacter(CharacterImportedConfigSet configs)
        {
            CharacterConfig[] characters = configs?.Characters ?? Array.Empty<CharacterConfig>();
            return characters.Length == 0 ? null : characters[0];
        }
    }
}

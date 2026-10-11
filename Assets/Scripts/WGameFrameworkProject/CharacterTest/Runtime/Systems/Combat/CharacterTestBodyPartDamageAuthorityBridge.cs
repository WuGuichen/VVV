using System;
using MxFramework.Combat.Damage;
using MxFramework.Combat.Hit;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestBodyPartHitProvenance
    {
        NoPartEvidence = 0,
        ExplicitColliderBinding = 1,
        DefaultFallback = 2,
        InvalidEvidence = 3
    }

    internal enum CharacterTestBodyPartDamageSourcePolicy
    {
        Unsupported = 0,
        DirectTrace = 1,
        Projectile = 2,
        Ability = 3,
        NoPartArea = 4,
        StatusTick = 5
    }

    internal enum CharacterTestBodyPartDamageIgnoredReason
    {
        None = 0,
        NoDefinitions = 1,
        NoEvidence = 2,
        DefaultFallback = 3,
        InvalidEvidence = 4,
        UnsupportedSource = 5,
        NoPartArea = 6,
        StatusTick = 7,
        RejectedHit = 8,
        ZeroFinalDamage = 9,
        Duplicate = 10,
        CapacityOverflow = 11,
        FrameMismatch = 12,
        TargetDead = 13,
        Invincible = 14,
        Parried = 15,
        Blocked = 16,
        SelfTarget = 17,
        FriendlyTarget = 18,
        UnmatchedResolvedHit = 19,
        StageRejected = 20,
        CommitRejected = 21,
        Evaded = 22
    }

    internal enum CharacterTestBodyPartFocusConsumeIngressResult
    {
        Staged = 0,
        AuthorityRejected = 1,
        UnsupportedAdapter = 2,
        InvalidIdentity = 3,
        FrameMismatch = 4,
        NoDefinitions = 5,
        UnknownPartState = 6,
        TagMismatch = 7,
        StateNotOpenAtFrameStart = 8,
        Duplicate = 9,
        CapacityOverflow = 10,
        StageRejected = 11
    }

    internal readonly struct CharacterTestBodyPartFocusConsumeIngress
    {
        public CharacterTestBodyPartFocusConsumeIngress(
            long authorityFrame,
            CharacterTestBodyPartFocusConsumeAdapterKind adapterKind,
            int sourceEntityId,
            int targetEntityId,
            int partStateId,
            string focusConsumeTag,
            long correlationId,
            bool authorityAccepted)
        {
            AuthorityFrame = authorityFrame;
            AdapterKind = adapterKind;
            SourceEntityId = sourceEntityId;
            TargetEntityId = targetEntityId;
            PartStateId = partStateId;
            FocusConsumeTag = focusConsumeTag ?? string.Empty;
            CorrelationId = correlationId;
            AuthorityAccepted = authorityAccepted;
        }

        public long AuthorityFrame { get; }
        public CharacterTestBodyPartFocusConsumeAdapterKind AdapterKind { get; }
        public int SourceEntityId { get; }
        public int TargetEntityId { get; }
        public int PartStateId { get; }
        public string FocusConsumeTag { get; }
        public long CorrelationId { get; }
        public bool AuthorityAccepted { get; }
    }

    internal enum CharacterTestBodyPartSeverIngressResult
    {
        Staged = 0,
        AuthorityRejected = 1,
        InvalidIdentity = 2,
        FrameMismatch = 3,
        NoDefinitions = 4,
        UnknownPartState = 5,
        NotSeverable = 6,
        TagMismatch = 7,
        StateNotBrokenAtFrameStart = 8,
        Duplicate = 9,
        CapacityOverflow = 10,
        StageRejected = 11
    }

    internal readonly struct CharacterTestBodyPartSeverIngress
    {
        public CharacterTestBodyPartSeverIngress(
            long authorityFrame,
            int sourceEntityId,
            int targetEntityId,
            int partStateId,
            string severTag,
            long correlationId,
            bool authorityAccepted)
        {
            AuthorityFrame = authorityFrame;
            SourceEntityId = sourceEntityId;
            TargetEntityId = targetEntityId;
            PartStateId = partStateId;
            SeverTag = severTag ?? string.Empty;
            CorrelationId = correlationId;
            AuthorityAccepted = authorityAccepted;
        }

        public long AuthorityFrame { get; }
        public int SourceEntityId { get; }
        public int TargetEntityId { get; }
        public int PartStateId { get; }
        public string SeverTag { get; }
        public long CorrelationId { get; }
        public bool AuthorityAccepted { get; }
    }

    internal readonly struct CharacterTestBodyPartHitEvidence
    {
        public CharacterTestBodyPartHitEvidence(
            CharacterTestHitZoneResolution hitZone,
            CharacterTestBodyPartHitProvenance provenance)
        {
            HitZone = hitZone;
            Provenance = provenance;
        }

        public CharacterTestHitZoneResolution HitZone { get; }
        public CharacterTestBodyPartHitProvenance Provenance { get; }
    }

    internal readonly struct CharacterTestBodyPartPreHitDecision
    {
        public CharacterTestBodyPartPreHitDecision(
            CharacterTestBodyPartDamageSourcePolicy sourcePolicy,
            CharacterTestBodyPartHitProvenance provenance,
            CharacterTestBodyPartDamageIgnoredReason ignoredReason,
            int definitionIndex,
            CharacterTestBodyPartStateEntry frameStartState,
            int scaleMilli,
            int baseDamage,
            int resolvedDamage)
        {
            SourcePolicy = sourcePolicy;
            Provenance = provenance;
            IgnoredReason = ignoredReason;
            DefinitionIndex = definitionIndex;
            FrameStartState = frameStartState;
            ScaleMilli = scaleMilli;
            BaseDamage = baseDamage;
            ResolvedDamage = resolvedDamage;
        }

        public CharacterTestBodyPartDamageSourcePolicy SourcePolicy { get; }
        public CharacterTestBodyPartHitProvenance Provenance { get; }
        public CharacterTestBodyPartDamageIgnoredReason IgnoredReason { get; }
        public int DefinitionIndex { get; }
        public CharacterTestBodyPartStateEntry FrameStartState { get; }
        public int ScaleMilli { get; }
        public int BaseDamage { get; }
        public int ResolvedDamage { get; }
        public bool Applied => IgnoredReason == CharacterTestBodyPartDamageIgnoredReason.None
            && ScaleMilli != 1000;
    }

    internal readonly struct CharacterTestBodyPartDamageTelemetryProjection
    {
        internal CharacterTestBodyPartDamageTelemetryProjection(
            long authorityFrame,
            CharacterTestResolvedHitKey hitKey,
            CharacterTestBodyPartDamageSourcePolicy sourcePolicy,
            CharacterTestBodyPartHitProvenance provenance,
            CharacterTestBodyPartDamageIgnoredReason ignoredReason,
            int definitionIndex,
            int baseDamage,
            int resolvedDamage,
            int finalDamage,
            CharacterTestBodyPartStateEntry frameStartState,
            CharacterTestBodyPartStateEntry committedState,
            bool hasCommittedState)
        {
            AuthorityFrame = authorityFrame;
            HitKey = hitKey;
            SourcePolicy = sourcePolicy;
            Provenance = provenance;
            IgnoredReason = ignoredReason;
            DefinitionIndex = definitionIndex;
            BaseDamage = baseDamage;
            ResolvedDamage = resolvedDamage;
            FinalDamage = finalDamage;
            FrameStartState = frameStartState;
            CommittedState = committedState;
            HasCommittedState = hasCommittedState;
        }

        public long AuthorityFrame { get; }
        public CharacterTestResolvedHitKey HitKey { get; }
        public CharacterTestBodyPartDamageSourcePolicy SourcePolicy { get; }
        public CharacterTestBodyPartHitProvenance Provenance { get; }
        public CharacterTestBodyPartDamageIgnoredReason IgnoredReason { get; }
        public int DefinitionIndex { get; }
        public int BaseDamage { get; }
        public int ResolvedDamage { get; }
        public int FinalDamage { get; }
        public CharacterTestBodyPartStateEntry FrameStartState { get; }
        public CharacterTestBodyPartStateEntry CommittedState { get; }
        public bool HasCommittedState { get; }

        internal CharacterTestBodyPartDamageTelemetryProjection WithCommitResult(
            CharacterTestBodyPartDamageIgnoredReason ignoredReason,
            CharacterTestBodyPartStateEntry committedState,
            bool hasCommittedState)
        {
            return new CharacterTestBodyPartDamageTelemetryProjection(
                AuthorityFrame,
                HitKey,
                SourcePolicy,
                Provenance,
                ignoredReason,
                DefinitionIndex,
                BaseDamage,
                ResolvedDamage,
                FinalDamage,
                FrameStartState,
                committedState,
                hasCommittedState);
        }
    }

    internal readonly struct CharacterTestBodyPartTransitionProjection
    {
        internal CharacterTestBodyPartTransitionProjection(
            CharacterTestBodyPartStateTransition transition,
            string breakReactionId,
            string presentationEventId)
        {
            Transition = transition;
            BreakReactionId = breakReactionId ?? string.Empty;
            PresentationEventId = presentationEventId ?? string.Empty;
        }

        public CharacterTestBodyPartStateTransition Transition { get; }
        public string BreakReactionId { get; }
        public string PresentationEventId { get; }
        public long AuthorityEventRevision => Transition.After.Revision;
        public bool HasBreakReaction =>
            Transition.Reason == CharacterTestBodyPartTransitionReason.Broken
            && !string.IsNullOrEmpty(BreakReactionId);
        public bool HasSeveredPresentationEvent =>
            Transition.Reason == CharacterTestBodyPartTransitionReason.Severed
            && !string.IsNullOrEmpty(PresentationEventId);
    }

    /// <summary>
    /// CharacterTest-only seam between resolved hit evidence, HP authority acceptance,
    /// and the body-part state reducer. The implementation is intentionally kept out
    /// of the shared combat event contracts.
    /// </summary>
    internal sealed class CharacterTestBodyPartDamageAuthorityBridge
    {
        private const int DamageScaleOneMilli = 1000;
        private const int MaximumFrameTargetCount = 128;
        private const int MaximumResolvedHitCount = 256;
        private const int MaximumDamageEventCount = 256;
        private const int MaximumFocusConsumeIngressCount = 64;
        private const int MaximumSeverIngressCount = 64;
        private const int MaximumTransitionProjectionCount =
            (MaximumResolvedHitCount * 2) + MaximumFocusConsumeIngressCount
            + MaximumSeverIngressCount;

        private readonly FrameTarget[] _frameTargets =
            new FrameTarget[MaximumFrameTargetCount];
        private readonly ResolvedHitRecord[] _resolvedHits =
            new ResolvedHitRecord[MaximumResolvedHitCount];
        private readonly DamageResolvedEvent[] _damageEvents =
            new DamageResolvedEvent[MaximumDamageEventCount];
        private readonly HitResolveResult[] _hitOutcomes =
            new HitResolveResult[MaximumResolvedHitCount];
        private readonly FocusConsumeIngressRecord[] _focusConsumeIngresses =
            new FocusConsumeIngressRecord[MaximumFocusConsumeIngressCount];
        private readonly SeverIngressRecord[] _severIngresses =
            new SeverIngressRecord[MaximumSeverIngressCount];
        private readonly CharacterTestBodyPartDamageTelemetryProjection[] _damageTelemetry =
            new CharacterTestBodyPartDamageTelemetryProjection[MaximumDamageEventCount];
        private readonly int[] _damageTelemetryRecordIndices =
            new int[MaximumDamageEventCount];
        private readonly CharacterTestBodyPartTransitionProjection[] _transitionProjections =
            new CharacterTestBodyPartTransitionProjection[MaximumTransitionProjectionCount];
        private long _authorityFrame = -1L;
        private int _frameTargetCount;
        private int _resolvedHitCount;
        private int _damageEventCount;
        private int _hitOutcomeCount;
        private int _focusConsumeIngressCount;
        private int _severIngressCount;
        private int _damageTelemetryCount;
        private int _transitionProjectionCount;
        private bool _frameActive;
        private long _projectionAuthorityFrame = -1L;

        internal int DamageTelemetryCount => _damageTelemetryCount;
        internal int TransitionProjectionCount => _transitionProjectionCount;

        /// <summary>Drops discarded-timeline frame staging and derived projections after authority restore.</summary>
        /// <remarks>NoAlloc. Definition bindings and committed body-part authority live outside this bridge.</remarks>
        internal void ResetForAuthorityRestore()
        {
            _authorityFrame = -1L;
            _frameTargetCount = 0;
            _resolvedHitCount = 0;
            _damageEventCount = 0;
            _hitOutcomeCount = 0;
            _focusConsumeIngressCount = 0;
            _severIngressCount = 0;
            _damageTelemetryCount = 0;
            _transitionProjectionCount = 0;
            _frameActive = false;
            _projectionAuthorityFrame = -1L;
        }

        internal CharacterTestBodyPartDamageTelemetryProjection GetDamageTelemetryAt(int index)
        {
            return index >= 0 && index < _damageTelemetryCount
                ? _damageTelemetry[index]
                : default;
        }

        internal CharacterTestBodyPartTransitionProjection GetTransitionProjectionAt(int index)
        {
            return index >= 0 && index < _transitionProjectionCount
                ? _transitionProjections[index]
                : default;
        }

        internal bool BeginAuthorityFrame(long authorityFrame)
        {
            if (authorityFrame < 0L)
                return false;
            if (_frameActive)
                return _authorityFrame == authorityFrame;

            _authorityFrame = authorityFrame;
            _frameTargetCount = 0;
            _resolvedHitCount = 0;
            _damageEventCount = 0;
            _hitOutcomeCount = 0;
            _focusConsumeIngressCount = 0;
            _severIngressCount = 0;
            _damageTelemetryCount = 0;
            _transitionProjectionCount = 0;
            _frameActive = true;
            return true;
        }

        internal DamageProfileResolveResult ResolvePreHitDamage(
            long authorityFrame,
            CharacterTestBodyPartStateComponent state,
            in CharacterTestBodyPartHitEvidence evidence,
            CharacterTestBodyPartDamageSourcePolicy sourcePolicy,
            in DamageProfileResolveResult damage,
            out CharacterTestBodyPartPreHitDecision decision)
        {
            if ((!_frameActive && !BeginAuthorityFrame(authorityFrame))
                || _authorityFrame != authorityFrame)
            {
                decision = CreateIgnoredDecision(
                    sourcePolicy,
                    evidence.Provenance,
                    CharacterTestBodyPartDamageIgnoredReason.FrameMismatch,
                    damage.IncomingDamage);
                return damage;
            }

            CharacterTestBodyPartDamageIgnoredReason ignoredReason =
                ValidateEvidence(sourcePolicy, evidence.Provenance);
            if (ignoredReason != CharacterTestBodyPartDamageIgnoredReason.None)
            {
                decision = CreateIgnoredDecision(
                    sourcePolicy,
                    evidence.Provenance,
                    ignoredReason,
                    damage.IncomingDamage);
                return damage;
            }

            if (state == null
                || state.InitializationStatus == CharacterTestBodyPartStateInitializationStatus.MissingDefinitions)
            {
                decision = CreateIgnoredDecision(
                    sourcePolicy,
                    evidence.Provenance,
                    CharacterTestBodyPartDamageIgnoredReason.NoDefinitions,
                    damage.IncomingDamage);
                return damage;
            }

            if (state.InitializationStatus != CharacterTestBodyPartStateInitializationStatus.Ready)
            {
                decision = CreateIgnoredDecision(
                    sourcePolicy,
                    evidence.Provenance,
                    CharacterTestBodyPartDamageIgnoredReason.InvalidEvidence,
                    damage.IncomingDamage);
                return damage;
            }

            if (!TryGetFrameStartView(
                    authorityFrame,
                    state,
                    out CharacterTestBodyPartStateReadView frameStartView,
                    out ignoredReason))
            {
                decision = CreateIgnoredDecision(
                    sourcePolicy,
                    evidence.Provenance,
                    ignoredReason,
                    damage.IncomingDamage);
                return damage;
            }

            if (!state.TryResolveDefinitionIndex(
                    evidence.HitZone.BodyPartId,
                    out int definitionIndex,
                    out CharacterTestBodyPartEvidenceReason evidenceReason))
            {
                ignoredReason = evidenceReason == CharacterTestBodyPartEvidenceReason.NoDefinitions
                    ? CharacterTestBodyPartDamageIgnoredReason.NoDefinitions
                    : evidenceReason == CharacterTestBodyPartEvidenceReason.InvalidBinding
                        ? CharacterTestBodyPartDamageIgnoredReason.InvalidEvidence
                        : CharacterTestBodyPartDamageIgnoredReason.NoEvidence;
                decision = CreateIgnoredDecision(
                    sourcePolicy,
                    evidence.Provenance,
                    ignoredReason,
                    damage.IncomingDamage);
                return damage;
            }

            if (!frameStartView.TryGet(definitionIndex, out CharacterTestBodyPartStateEntry entry)
                || !state.TryGetDefinition(definitionIndex, out CharacterTestBodyPartStateDefinition definition))
            {
                decision = CreateIgnoredDecision(
                    sourcePolicy,
                    evidence.Provenance,
                    CharacterTestBodyPartDamageIgnoredReason.InvalidEvidence,
                    damage.IncomingDamage);
                return damage;
            }

            int scaleMilli = entry.WoundState == CharacterTestBodyPartWoundState.Open
                ? definition.OpenDamageScaleMilli
                : DamageScaleOneMilli;
            if (scaleMilli <= 0)
            {
                decision = CreateIgnoredDecision(
                    sourcePolicy,
                    evidence.Provenance,
                    CharacterTestBodyPartDamageIgnoredReason.InvalidEvidence,
                    damage.IncomingDamage);
                return damage;
            }

            int resolvedDamage = ScaleDamage(damage.IncomingDamage, scaleMilli);
            decision = new CharacterTestBodyPartPreHitDecision(
                sourcePolicy,
                evidence.Provenance,
                CharacterTestBodyPartDamageIgnoredReason.None,
                definitionIndex,
                entry,
                scaleMilli,
                damage.IncomingDamage,
                resolvedDamage);
            return resolvedDamage == damage.IncomingDamage
                ? damage
                : WithIncomingDamage(damage, resolvedDamage);
        }

        internal bool RecordResolvedHit(
            in CharacterTestResolvedHitKey hitKey,
            CharacterTestBodyPartStateComponent state,
            in CharacterTestBodyPartHitEvidence evidence,
            CharacterTestBodyPartDamageSourcePolicy sourcePolicy,
            int sourceEntityId,
            int targetEntityId,
            int policyEventId,
            int localFrame,
            in CharacterTestBodyPartPreHitDecision preHitDecision)
        {
            if (!_frameActive
                || _authorityFrame != hitKey.Frame.Value
                || (sourceEntityId <= 0
                    && preHitDecision.IgnoredReason == CharacterTestBodyPartDamageIgnoredReason.None)
                || targetEntityId <= 0
                || policyEventId <= 0
                || localFrame < 0)
            {
                return false;
            }

            for (int i = 0; i < _resolvedHitCount; i++)
            {
                ResolvedHitRecord existing = _resolvedHits[i];
                if (!existing.HitKey.Equals(hitKey))
                    continue;
                if (existing.HasSameAuthorityIdentity(
                        state,
                        evidence,
                        sourcePolicy,
                        sourceEntityId,
                        targetEntityId,
                        policyEventId,
                        localFrame,
                        preHitDecision))
                {
                    return true;
                }

                _resolvedHits[i] = existing.WithAmbiguousIdentity();
                return false;
            }

            if (_resolvedHitCount >= _resolvedHits.Length)
                return false;

            _resolvedHits[_resolvedHitCount++] = new ResolvedHitRecord(
                hitKey,
                state,
                evidence,
                sourcePolicy,
                sourceEntityId,
                targetEntityId,
                policyEventId,
                localFrame,
                preHitDecision);
            return true;
        }

        /// <summary>
        /// DamageResolvedEvent publication happens before HP attribute commands run.
        /// This method therefore only copies into a fixed frame buffer.
        /// </summary>
        internal bool CacheResolvedDamageEvent(in DamageResolvedEvent damageEvent)
        {
            if (!_frameActive || _damageEventCount >= _damageEvents.Length)
                return false;

            _damageEvents[_damageEventCount++] = damageEvent;
            return true;
        }

        internal bool CacheResolvedHitOutcome(in HitResolveResult outcome)
        {
            if (!_frameActive || _authorityFrame != outcome.Frame.Value)
                return false;

            for (int i = 0; i < _hitOutcomeCount; i++)
            {
                if (_hitOutcomes[i].Equals(outcome))
                    return true;
            }
            if (_hitOutcomeCount >= _hitOutcomes.Length)
                return false;

            _hitOutcomes[_hitOutcomeCount++] = outcome;
            return true;
        }

        internal CharacterTestBodyPartFocusConsumeIngressResult StageAcceptedFocusConsume(
            CharacterTestBodyPartStateComponent state,
            in CharacterTestBodyPartFocusConsumeIngress ingress)
        {
            if (!ingress.AuthorityAccepted)
                return CharacterTestBodyPartFocusConsumeIngressResult.AuthorityRejected;
            if (!IsSupportedFocusAdapter(ingress.AdapterKind))
                return CharacterTestBodyPartFocusConsumeIngressResult.UnsupportedAdapter;
            if (state == null
                || ingress.AuthorityFrame < 0L
                || ingress.SourceEntityId <= 0
                || ingress.TargetEntityId <= 0
                || ingress.SourceEntityId == ingress.TargetEntityId
                || state.OwnerEntityId != ingress.TargetEntityId
                || ingress.PartStateId <= 0
                || string.IsNullOrEmpty(ingress.FocusConsumeTag)
                || ingress.CorrelationId <= 0L)
            {
                return CharacterTestBodyPartFocusConsumeIngressResult.InvalidIdentity;
            }
            if (state.InitializationStatus == CharacterTestBodyPartStateInitializationStatus.MissingDefinitions)
                return CharacterTestBodyPartFocusConsumeIngressResult.NoDefinitions;
            if (state.InitializationStatus != CharacterTestBodyPartStateInitializationStatus.Ready)
                return CharacterTestBodyPartFocusConsumeIngressResult.UnknownPartState;
            if (!TryGetFrameStartView(
                    ingress.AuthorityFrame,
                    state,
                    out CharacterTestBodyPartStateReadView frameStartView,
                    out _))
            {
                return CharacterTestBodyPartFocusConsumeIngressResult.FrameMismatch;
            }

            int definitionIndex = ResolveDefinitionIndexByPartStateId(
                state,
                ingress.PartStateId);
            if (definitionIndex < 0
                || !state.TryGetDefinition(
                    definitionIndex,
                    out CharacterTestBodyPartStateDefinition definition))
            {
                return CharacterTestBodyPartFocusConsumeIngressResult.UnknownPartState;
            }
            if (!string.Equals(
                    definition.FocusConsumeTag,
                    ingress.FocusConsumeTag,
                    StringComparison.Ordinal))
            {
                return CharacterTestBodyPartFocusConsumeIngressResult.TagMismatch;
            }
            if (!frameStartView.TryGet(definitionIndex, out CharacterTestBodyPartStateEntry entry)
                || entry.WoundState != CharacterTestBodyPartWoundState.Open)
            {
                return CharacterTestBodyPartFocusConsumeIngressResult.StateNotOpenAtFrameStart;
            }

            var record = new FocusConsumeIngressRecord(ingress, definitionIndex);
            for (int i = 0; i < _focusConsumeIngressCount; i++)
            {
                if (_focusConsumeIngresses[i].HasSameIdentity(record))
                    return CharacterTestBodyPartFocusConsumeIngressResult.Duplicate;
            }
            if (_focusConsumeIngressCount >= _focusConsumeIngresses.Length)
                return CharacterTestBodyPartFocusConsumeIngressResult.CapacityOverflow;

            CharacterTestBodyPartStateStageResult stageResult = state.StageFocusConsume(
                new CharacterTestBodyPartFocusConsumeFact(
                    ingress.AuthorityFrame,
                    ingress.AdapterKind,
                    ingress.SourceEntityId,
                    ingress.TargetEntityId,
                    definitionIndex,
                    ingress.FocusConsumeTag,
                    ingress.CorrelationId,
                    authorityAccepted: true));
            if (stageResult != CharacterTestBodyPartStateStageResult.Staged)
                return CharacterTestBodyPartFocusConsumeIngressResult.StageRejected;

            _focusConsumeIngresses[_focusConsumeIngressCount++] = record;
            return CharacterTestBodyPartFocusConsumeIngressResult.Staged;
        }

        internal CharacterTestBodyPartSeverIngressResult StageAcceptedSever(
            CharacterTestBodyPartStateComponent state,
            in CharacterTestBodyPartSeverIngress ingress)
        {
            if (!ingress.AuthorityAccepted)
                return CharacterTestBodyPartSeverIngressResult.AuthorityRejected;
            if (state == null
                || ingress.AuthorityFrame < 0L
                || ingress.SourceEntityId <= 0
                || ingress.TargetEntityId <= 0
                || ingress.SourceEntityId == ingress.TargetEntityId
                || state.OwnerEntityId != ingress.TargetEntityId
                || ingress.PartStateId <= 0
                || string.IsNullOrEmpty(ingress.SeverTag)
                || ingress.CorrelationId <= 0L)
            {
                return CharacterTestBodyPartSeverIngressResult.InvalidIdentity;
            }
            if (state.InitializationStatus == CharacterTestBodyPartStateInitializationStatus.MissingDefinitions)
                return CharacterTestBodyPartSeverIngressResult.NoDefinitions;
            if (state.InitializationStatus != CharacterTestBodyPartStateInitializationStatus.Ready)
                return CharacterTestBodyPartSeverIngressResult.UnknownPartState;
            if (!TryGetFrameStartView(
                    ingress.AuthorityFrame,
                    state,
                    out CharacterTestBodyPartStateReadView frameStartView,
                    out _))
            {
                return CharacterTestBodyPartSeverIngressResult.FrameMismatch;
            }

            if (!state.TryResolveDefinitionIndexByPartStateId(
                    ingress.PartStateId,
                    out int definitionIndex)
                || !state.TryGetDefinition(
                    definitionIndex,
                    out CharacterTestBodyPartStateDefinition definition))
            {
                return CharacterTestBodyPartSeverIngressResult.UnknownPartState;
            }
            if (!definition.Severable)
                return CharacterTestBodyPartSeverIngressResult.NotSeverable;
            if (!string.Equals(
                    definition.SeverRequiredTag,
                    ingress.SeverTag,
                    StringComparison.Ordinal))
            {
                return CharacterTestBodyPartSeverIngressResult.TagMismatch;
            }
            if (!frameStartView.TryGet(definitionIndex, out CharacterTestBodyPartStateEntry entry)
                || entry.StructureState != CharacterTestBodyPartStructureState.Broken)
            {
                return CharacterTestBodyPartSeverIngressResult.StateNotBrokenAtFrameStart;
            }

            var record = new SeverIngressRecord(ingress, definitionIndex);
            for (int i = 0; i < _severIngressCount; i++)
            {
                if (_severIngresses[i].HasSameIdentity(record))
                    return CharacterTestBodyPartSeverIngressResult.Duplicate;
            }
            if (_severIngressCount >= _severIngresses.Length)
                return CharacterTestBodyPartSeverIngressResult.CapacityOverflow;

            CharacterTestBodyPartStateStageResult stageResult = state.StageSever(
                new CharacterTestBodyPartSeverFact(
                    ingress.AuthorityFrame,
                    ingress.SourceEntityId,
                    ingress.TargetEntityId,
                    definitionIndex,
                    ingress.SeverTag,
                    ingress.CorrelationId,
                    authorityAccepted: true));
            if (stageResult != CharacterTestBodyPartStateStageResult.Staged)
                return CharacterTestBodyPartSeverIngressResult.StageRejected;

            _severIngresses[_severIngressCount++] = record;
            return CharacterTestBodyPartSeverIngressResult.Staged;
        }

        internal bool CompleteAuthorityFrame(long authorityFrame)
        {
            if (!_frameActive)
            {
                if (authorityFrame < 0L || authorityFrame < _projectionAuthorityFrame)
                    return false;
                if (authorityFrame > _projectionAuthorityFrame)
                {
                    _damageTelemetryCount = 0;
                    _transitionProjectionCount = 0;
                    _projectionAuthorityFrame = authorityFrame;
                }
                return true;
            }
            if (_authorityFrame != authorityFrame)
                return false;

            for (int i = 0; i < _damageEventCount; i++)
                StageAcceptedDamage(_damageEvents[i]);
            for (int i = 0; i < _hitOutcomeCount; i++)
                AppendRejectedOutcomeTelemetry(_hitOutcomes[i]);

            bool succeeded = true;
            for (int i = 0; i < _frameTargetCount; i++)
            {
                FrameTarget target = _frameTargets[i];
                CharacterTestBodyPartStateCommitResult commit =
                    target.State.CommitFrame();
                succeeded &= commit.Succeeded;
                if (commit.Succeeded)
                    CopyCommittedTransitions(target.State);
                else
                    MarkTargetCommitRejected(target.State);
            }

            FinalizeDamageTelemetry();

            _frameTargetCount = 0;
            _resolvedHitCount = 0;
            _damageEventCount = 0;
            _hitOutcomeCount = 0;
            _focusConsumeIngressCount = 0;
            _severIngressCount = 0;
            _authorityFrame = -1L;
            _frameActive = false;
            _projectionAuthorityFrame = authorityFrame;
            return succeeded;
        }

        private void StageAcceptedDamage(in DamageResolvedEvent damageEvent)
        {
            int recordIndex = FindUniqueResolvedHit(damageEvent);
            if (recordIndex < 0)
            {
                AppendUnmatchedDamageTelemetry(damageEvent);
                return;
            }

            ResolvedHitRecord record = _resolvedHits[recordIndex];
            CharacterTestBodyPartDamageIgnoredReason ignoredReason =
                ResolveAcceptedDamageIgnoredReason(damageEvent, record);
            if (ignoredReason != CharacterTestBodyPartDamageIgnoredReason.None)
            {
                AppendDamageTelemetry(recordIndex, damageEvent, ignoredReason);
                return;
            }

            CharacterTestBodyPartStateStageResult stageResult =
                record.State.StageAcceptedDamage(
                    new CharacterTestBodyPartAcceptedDamageFact(
                        _authorityFrame,
                        record.SourceEntityId,
                        record.TargetEntityId,
                        damageEvent.HitResult.ActionInstanceId,
                        record.PolicyEventId,
                        damageEvent.HitResult.TraceId,
                        record.LocalFrame,
                        record.PreHitDecision.DefinitionIndex,
                        damageEvent.FinalDamage,
                        authorityAccepted: true));
            if (stageResult == CharacterTestBodyPartStateStageResult.Staged)
                _resolvedHits[recordIndex] = record.WithConsumedEvent();
            else
                ignoredReason = stageResult == CharacterTestBodyPartStateStageResult.CapacityOverflow
                    ? CharacterTestBodyPartDamageIgnoredReason.CapacityOverflow
                    : CharacterTestBodyPartDamageIgnoredReason.StageRejected;

            AppendDamageTelemetry(recordIndex, damageEvent, ignoredReason);
        }

        private static CharacterTestBodyPartDamageIgnoredReason ResolveAcceptedDamageIgnoredReason(
            in DamageResolvedEvent damageEvent,
            in ResolvedHitRecord record)
        {
            if (record.PreHitDecision.IgnoredReason != CharacterTestBodyPartDamageIgnoredReason.None)
                return record.PreHitDecision.IgnoredReason;
            if (record.HasConsumedEvent)
                return CharacterTestBodyPartDamageIgnoredReason.Duplicate;

            CharacterTestBodyPartDamageIgnoredReason hitReason =
                ResolveHitKindIgnoredReason(damageEvent.HitResult);
            if (hitReason != CharacterTestBodyPartDamageIgnoredReason.None)
                return hitReason;

            if (damageEvent.FinalDamage <= 0)
                return CharacterTestBodyPartDamageIgnoredReason.ZeroFinalDamage;
            if (record.SourceEntityId <= 0
                || record.TargetEntityId <= 0
                || record.State == null
                || record.State.OwnerEntityId != record.TargetEntityId)
            {
                return CharacterTestBodyPartDamageIgnoredReason.InvalidEvidence;
            }
            if (record.SourceEntityId == record.TargetEntityId)
                return CharacterTestBodyPartDamageIgnoredReason.SelfTarget;
            return CharacterTestBodyPartDamageIgnoredReason.None;
        }

        private void AppendRejectedOutcomeTelemetry(in HitResolveResult outcome)
        {
            if (outcome.Kind == HitResolveKind.Damage
                || outcome.Kind == HitResolveKind.Blocked)
            {
                return;
            }

            var hitKey = new CharacterTestResolvedHitKey(outcome);
            for (int i = 0; i < _damageTelemetryCount; i++)
            {
                if (_damageTelemetry[i].HitKey.Equals(hitKey))
                    return;
            }

            int recordIndex = FindUniqueResolvedHit(hitKey);
            if (recordIndex < 0 || _damageTelemetryCount >= _damageTelemetry.Length)
                return;

            ResolvedHitRecord record = _resolvedHits[recordIndex];
            CharacterTestBodyPartDamageIgnoredReason ignoredReason =
                record.PreHitDecision.IgnoredReason != CharacterTestBodyPartDamageIgnoredReason.None
                    ? record.PreHitDecision.IgnoredReason
                    : ResolveHitKindIgnoredReason(outcome);
            _damageTelemetryRecordIndices[_damageTelemetryCount] = recordIndex;
            _damageTelemetry[_damageTelemetryCount++] =
                new CharacterTestBodyPartDamageTelemetryProjection(
                    _authorityFrame,
                    record.HitKey,
                    record.SourcePolicy,
                    record.Evidence.Provenance,
                    ignoredReason,
                    record.PreHitDecision.DefinitionIndex,
                    record.PreHitDecision.BaseDamage,
                    record.PreHitDecision.ResolvedDamage,
                    finalDamage: 0,
                    record.PreHitDecision.FrameStartState,
                    committedState: default,
                    hasCommittedState: false);
        }

        private static CharacterTestBodyPartDamageIgnoredReason ResolveHitKindIgnoredReason(
            in HitResolveResult outcome)
        {
            switch (outcome.Kind)
            {
                case HitResolveKind.Damage:
                    return outcome.IsAcceptedDamage
                        ? CharacterTestBodyPartDamageIgnoredReason.None
                        : CharacterTestBodyPartDamageIgnoredReason.RejectedHit;
                case HitResolveKind.TargetDead:
                    return CharacterTestBodyPartDamageIgnoredReason.TargetDead;
                case HitResolveKind.Invincible:
                    return CharacterTestBodyPartDamageIgnoredReason.Invincible;
                case HitResolveKind.Evaded:
                    return CharacterTestBodyPartDamageIgnoredReason.Evaded;
                case HitResolveKind.Parried:
                    return CharacterTestBodyPartDamageIgnoredReason.Parried;
                case HitResolveKind.Blocked:
                    return CharacterTestBodyPartDamageIgnoredReason.Blocked;
                case HitResolveKind.Duplicate:
                    return CharacterTestBodyPartDamageIgnoredReason.Duplicate;
                case HitResolveKind.SelfDamage:
                    return CharacterTestBodyPartDamageIgnoredReason.SelfTarget;
                case HitResolveKind.Friendly:
                    return CharacterTestBodyPartDamageIgnoredReason.FriendlyTarget;
                default:
                    return CharacterTestBodyPartDamageIgnoredReason.RejectedHit;
            }
        }

        private void AppendDamageTelemetry(
            int recordIndex,
            in DamageResolvedEvent damageEvent,
            CharacterTestBodyPartDamageIgnoredReason ignoredReason)
        {
            if (_damageTelemetryCount >= _damageTelemetry.Length)
                return;

            ResolvedHitRecord record = _resolvedHits[recordIndex];
            _damageTelemetryRecordIndices[_damageTelemetryCount] = recordIndex;
            _damageTelemetry[_damageTelemetryCount++] =
                new CharacterTestBodyPartDamageTelemetryProjection(
                    _authorityFrame,
                    record.HitKey,
                    record.SourcePolicy,
                    record.Evidence.Provenance,
                    ignoredReason,
                    record.PreHitDecision.DefinitionIndex,
                    record.PreHitDecision.BaseDamage,
                    record.PreHitDecision.ResolvedDamage,
                    damageEvent.FinalDamage,
                    record.PreHitDecision.FrameStartState,
                    committedState: default,
                    hasCommittedState: false);
        }

        private void AppendUnmatchedDamageTelemetry(in DamageResolvedEvent damageEvent)
        {
            if (_damageTelemetryCount >= _damageTelemetry.Length)
                return;

            _damageTelemetryRecordIndices[_damageTelemetryCount] = -1;
            _damageTelemetry[_damageTelemetryCount++] =
                new CharacterTestBodyPartDamageTelemetryProjection(
                    _authorityFrame,
                    new CharacterTestResolvedHitKey(damageEvent.HitResult),
                    CharacterTestBodyPartDamageSourcePolicy.Unsupported,
                    CharacterTestBodyPartHitProvenance.NoPartEvidence,
                    CharacterTestBodyPartDamageIgnoredReason.UnmatchedResolvedHit,
                    definitionIndex: -1,
                    baseDamage: damageEvent.ProfileResult.IncomingDamage,
                    resolvedDamage: damageEvent.ProfileResult.IncomingDamage,
                    damageEvent.FinalDamage,
                    frameStartState: default,
                    committedState: default,
                    hasCommittedState: false);
        }

        private void FinalizeDamageTelemetry()
        {
            for (int i = 0; i < _damageTelemetryCount; i++)
            {
                int recordIndex = _damageTelemetryRecordIndices[i];
                if (recordIndex < 0 || recordIndex >= _resolvedHitCount)
                    continue;

                ResolvedHitRecord record = _resolvedHits[recordIndex];
                if (record.State == null
                    || record.PreHitDecision.DefinitionIndex < 0
                    || !record.State.TryGetCurrentEntry(
                        record.PreHitDecision.DefinitionIndex,
                        out CharacterTestBodyPartStateEntry committedState))
                {
                    continue;
                }

                CharacterTestBodyPartDamageTelemetryProjection row = _damageTelemetry[i];
                _damageTelemetry[i] = row.WithCommitResult(
                    row.IgnoredReason,
                    committedState,
                    hasCommittedState: true);
            }
        }

        private void MarkTargetCommitRejected(CharacterTestBodyPartStateComponent state)
        {
            for (int i = 0; i < _damageTelemetryCount; i++)
            {
                int recordIndex = _damageTelemetryRecordIndices[i];
                if (recordIndex < 0
                    || recordIndex >= _resolvedHitCount
                    || !ReferenceEquals(_resolvedHits[recordIndex].State, state))
                {
                    continue;
                }

                CharacterTestBodyPartDamageTelemetryProjection row = _damageTelemetry[i];
                if (row.IgnoredReason == CharacterTestBodyPartDamageIgnoredReason.None)
                {
                    _damageTelemetry[i] = row.WithCommitResult(
                        CharacterTestBodyPartDamageIgnoredReason.CommitRejected,
                        committedState: default,
                        hasCommittedState: false);
                }
            }
        }

        private void CopyCommittedTransitions(CharacterTestBodyPartStateComponent state)
        {
            int count = state.TransitionCount;
            for (int i = 0; i < count; i++)
            {
                if (_transitionProjectionCount >= _transitionProjections.Length)
                    return;

                CharacterTestBodyPartStateTransition transition = state.GetTransitionAt(i);
                string breakReactionId = string.Empty;
                string presentationEventId = string.Empty;
                if (transition.Reason == CharacterTestBodyPartTransitionReason.Broken
                    && state.TryGetDefinition(
                        transition.DefinitionIndex,
                        out CharacterTestBodyPartStateDefinition definition))
                {
                    breakReactionId = definition.BreakReactionId;
                }
                else if (transition.Reason == CharacterTestBodyPartTransitionReason.Severed
                    && state.TryGetDefinition(
                        transition.DefinitionIndex,
                        out CharacterTestBodyPartStateDefinition severDefinition))
                {
                    presentationEventId = severDefinition.PresentationEventId;
                }

                _transitionProjections[_transitionProjectionCount++] =
                    new CharacterTestBodyPartTransitionProjection(
                        transition,
                        breakReactionId,
                        presentationEventId);
            }
        }

        private int FindUniqueResolvedHit(in DamageResolvedEvent damageEvent)
        {
            var eventKey = new CharacterTestResolvedHitKey(damageEvent.HitResult);
            int match = -1;
            for (int i = 0; i < _resolvedHitCount; i++)
            {
                if (!_resolvedHits[i].Matches(eventKey, damageEvent.ProfileResult))
                    continue;
                if (match >= 0)
                    return -1;
                match = i;
            }

            return match;
        }

        private int FindUniqueResolvedHit(in CharacterTestResolvedHitKey hitKey)
        {
            int match = -1;
            for (int i = 0; i < _resolvedHitCount; i++)
            {
                if (!_resolvedHits[i].Matches(hitKey))
                    continue;
                if (match >= 0)
                    return -1;
                match = i;
            }

            return match;
        }

        private bool TryGetFrameStartView(
            long authorityFrame,
            CharacterTestBodyPartStateComponent state,
            out CharacterTestBodyPartStateReadView view,
            out CharacterTestBodyPartDamageIgnoredReason ignoredReason)
        {
            if (!_frameActive && !BeginAuthorityFrame(authorityFrame))
            {
                view = default;
                ignoredReason = CharacterTestBodyPartDamageIgnoredReason.FrameMismatch;
                return false;
            }
            if (_authorityFrame != authorityFrame)
            {
                view = default;
                ignoredReason = CharacterTestBodyPartDamageIgnoredReason.FrameMismatch;
                return false;
            }

            for (int i = 0; i < _frameTargetCount; i++)
            {
                FrameTarget candidate = _frameTargets[i];
                if (ReferenceEquals(candidate.State, state))
                {
                    view = candidate.View;
                    ignoredReason = CharacterTestBodyPartDamageIgnoredReason.None;
                    return true;
                }
                if (candidate.OwnerEntityId == state.OwnerEntityId)
                {
                    view = default;
                    ignoredReason = CharacterTestBodyPartDamageIgnoredReason.InvalidEvidence;
                    return false;
                }
            }

            if (_frameTargetCount >= _frameTargets.Length)
            {
                view = default;
                ignoredReason = CharacterTestBodyPartDamageIgnoredReason.CapacityOverflow;
                return false;
            }
            if (!state.TryBeginFrame(authorityFrame, out view))
            {
                ignoredReason = CharacterTestBodyPartDamageIgnoredReason.FrameMismatch;
                return false;
            }

            _frameTargets[_frameTargetCount++] = new FrameTarget(
                state.OwnerEntityId,
                state,
                view);
            ignoredReason = CharacterTestBodyPartDamageIgnoredReason.None;
            return true;
        }

        private static CharacterTestBodyPartDamageIgnoredReason ValidateEvidence(
            CharacterTestBodyPartDamageSourcePolicy sourcePolicy,
            CharacterTestBodyPartHitProvenance provenance)
        {
            switch (sourcePolicy)
            {
                case CharacterTestBodyPartDamageSourcePolicy.DirectTrace:
                case CharacterTestBodyPartDamageSourcePolicy.Projectile:
                case CharacterTestBodyPartDamageSourcePolicy.Ability:
                    break;
                case CharacterTestBodyPartDamageSourcePolicy.NoPartArea:
                    return CharacterTestBodyPartDamageIgnoredReason.NoPartArea;
                case CharacterTestBodyPartDamageSourcePolicy.StatusTick:
                    return CharacterTestBodyPartDamageIgnoredReason.StatusTick;
                default:
                    return CharacterTestBodyPartDamageIgnoredReason.UnsupportedSource;
            }

            switch (provenance)
            {
                case CharacterTestBodyPartHitProvenance.ExplicitColliderBinding:
                    return CharacterTestBodyPartDamageIgnoredReason.None;
                case CharacterTestBodyPartHitProvenance.DefaultFallback:
                    return CharacterTestBodyPartDamageIgnoredReason.DefaultFallback;
                case CharacterTestBodyPartHitProvenance.InvalidEvidence:
                    return CharacterTestBodyPartDamageIgnoredReason.InvalidEvidence;
                default:
                    return CharacterTestBodyPartDamageIgnoredReason.NoEvidence;
            }
        }

        private static bool IsSupportedFocusAdapter(
            CharacterTestBodyPartFocusConsumeAdapterKind adapterKind)
        {
            return adapterKind == CharacterTestBodyPartFocusConsumeAdapterKind.Action
                || adapterKind == CharacterTestBodyPartFocusConsumeAdapterKind.Ability
                || adapterKind == CharacterTestBodyPartFocusConsumeAdapterKind.Interaction;
        }

        private static int ResolveDefinitionIndexByPartStateId(
            CharacterTestBodyPartStateComponent state,
            int partStateId)
        {
            int count = Math.Min(
                CharacterTestBodyPartStateComponent.MaximumDefinitionCount,
                state.DefinitionCount);
            for (int i = 0; i < count; i++)
            {
                if (state.TryGetDefinition(i, out CharacterTestBodyPartStateDefinition definition)
                    && definition.PartStateId == partStateId)
                {
                    return i;
                }
            }

            return -1;
        }

        private static int ScaleDamage(int damage, int scaleMilli)
        {
            if (damage <= 0 || scaleMilli <= 0)
                return 0;

            long scaled = ((long)damage * scaleMilli) / DamageScaleOneMilli;
            return scaled >= int.MaxValue ? int.MaxValue : (int)scaled;
        }

        private static DamageProfileResolveResult WithIncomingDamage(
            in DamageProfileResolveResult source,
            int incomingDamage)
        {
            return new DamageProfileResolveResult(
                source.Success,
                source.Reason,
                source.DamageProfileId,
                source.DamageTypeId,
                source.FormulaId,
                incomingDamage,
                source.StaggerFrames,
                source.ImpactForce,
                source.Knockback,
                source.ImpactPower,
                source.GuardPressure,
                source.PresentationProfileId,
                source.Usage,
                source.FormulaTrace,
                source.Tags,
                launchPower: source.LaunchPower,
                analysisCategory: source.AnalysisCategory);
        }

        private static CharacterTestBodyPartPreHitDecision CreateIgnoredDecision(
            CharacterTestBodyPartDamageSourcePolicy sourcePolicy,
            CharacterTestBodyPartHitProvenance provenance,
            CharacterTestBodyPartDamageIgnoredReason ignoredReason,
            int damage)
        {
            return new CharacterTestBodyPartPreHitDecision(
                sourcePolicy,
                provenance,
                ignoredReason,
                definitionIndex: -1,
                frameStartState: default,
                scaleMilli: DamageScaleOneMilli,
                baseDamage: damage,
                resolvedDamage: damage);
        }

        private readonly struct FrameTarget
        {
            public FrameTarget(
                int ownerEntityId,
                CharacterTestBodyPartStateComponent state,
                CharacterTestBodyPartStateReadView view)
            {
                OwnerEntityId = ownerEntityId;
                State = state;
                View = view;
            }

            public int OwnerEntityId { get; }
            public CharacterTestBodyPartStateComponent State { get; }
            public CharacterTestBodyPartStateReadView View { get; }
        }

        private readonly struct FocusConsumeIngressRecord
        {
            public FocusConsumeIngressRecord(
                CharacterTestBodyPartFocusConsumeIngress ingress,
                int definitionIndex)
            {
                AuthorityFrame = ingress.AuthorityFrame;
                AdapterKind = ingress.AdapterKind;
                SourceEntityId = ingress.SourceEntityId;
                TargetEntityId = ingress.TargetEntityId;
                DefinitionIndex = definitionIndex;
                CorrelationId = ingress.CorrelationId;
            }

            public long AuthorityFrame { get; }
            public CharacterTestBodyPartFocusConsumeAdapterKind AdapterKind { get; }
            public int SourceEntityId { get; }
            public int TargetEntityId { get; }
            public int DefinitionIndex { get; }
            public long CorrelationId { get; }

            public bool HasSameIdentity(in FocusConsumeIngressRecord other)
            {
                return AuthorityFrame == other.AuthorityFrame
                    && AdapterKind == other.AdapterKind
                    && SourceEntityId == other.SourceEntityId
                    && TargetEntityId == other.TargetEntityId
                    && DefinitionIndex == other.DefinitionIndex
                    && CorrelationId == other.CorrelationId;
            }
        }

        private readonly struct SeverIngressRecord
        {
            public SeverIngressRecord(
                CharacterTestBodyPartSeverIngress ingress,
                int definitionIndex)
            {
                AuthorityFrame = ingress.AuthorityFrame;
                SourceEntityId = ingress.SourceEntityId;
                TargetEntityId = ingress.TargetEntityId;
                DefinitionIndex = definitionIndex;
                CorrelationId = ingress.CorrelationId;
            }

            public long AuthorityFrame { get; }
            public int SourceEntityId { get; }
            public int TargetEntityId { get; }
            public int DefinitionIndex { get; }
            public long CorrelationId { get; }

            public bool HasSameIdentity(in SeverIngressRecord other)
            {
                return AuthorityFrame == other.AuthorityFrame
                    && SourceEntityId == other.SourceEntityId
                    && TargetEntityId == other.TargetEntityId
                    && DefinitionIndex == other.DefinitionIndex
                    && CorrelationId == other.CorrelationId;
            }
        }

        private readonly struct ResolvedHitRecord
        {
            public ResolvedHitRecord(
                CharacterTestResolvedHitKey hitKey,
                CharacterTestBodyPartStateComponent state,
                CharacterTestBodyPartHitEvidence evidence,
                CharacterTestBodyPartDamageSourcePolicy sourcePolicy,
                int sourceEntityId,
                int targetEntityId,
                int policyEventId,
                int localFrame,
                CharacterTestBodyPartPreHitDecision preHitDecision,
                bool hasAmbiguousIdentity = false,
                bool hasConsumedEvent = false)
            {
                HitKey = hitKey;
                State = state;
                Evidence = evidence;
                SourcePolicy = sourcePolicy;
                SourceEntityId = sourceEntityId;
                TargetEntityId = targetEntityId;
                PolicyEventId = policyEventId;
                LocalFrame = localFrame;
                PreHitDecision = preHitDecision;
                HasAmbiguousIdentity = hasAmbiguousIdentity;
                HasConsumedEvent = hasConsumedEvent;
            }

            public CharacterTestResolvedHitKey HitKey { get; }
            public CharacterTestBodyPartStateComponent State { get; }
            public CharacterTestBodyPartHitEvidence Evidence { get; }
            public CharacterTestBodyPartDamageSourcePolicy SourcePolicy { get; }
            public int SourceEntityId { get; }
            public int TargetEntityId { get; }
            public int PolicyEventId { get; }
            public int LocalFrame { get; }
            public CharacterTestBodyPartPreHitDecision PreHitDecision { get; }
            public bool HasAmbiguousIdentity { get; }
            public bool HasConsumedEvent { get; }

            public bool HasSameAuthorityIdentity(
                CharacterTestBodyPartStateComponent state,
                in CharacterTestBodyPartHitEvidence evidence,
                CharacterTestBodyPartDamageSourcePolicy sourcePolicy,
                int sourceEntityId,
                int targetEntityId,
                int policyEventId,
                int localFrame,
                in CharacterTestBodyPartPreHitDecision preHitDecision)
            {
                return !HasAmbiguousIdentity
                    && ReferenceEquals(State, state)
                    && SourcePolicy == sourcePolicy
                    && Evidence.Provenance == evidence.Provenance
                    && Evidence.HitZone.HitZoneId == evidence.HitZone.HitZoneId
                    && Evidence.HitZone.BodyPartId == evidence.HitZone.BodyPartId
                    && SourceEntityId == sourceEntityId
                    && TargetEntityId == targetEntityId
                    && PolicyEventId == policyEventId
                    && LocalFrame == localFrame
                    && PreHitDecision.DefinitionIndex == preHitDecision.DefinitionIndex
                    && PreHitDecision.BaseDamage == preHitDecision.BaseDamage
                    && PreHitDecision.ResolvedDamage == preHitDecision.ResolvedDamage
                    && PreHitDecision.IgnoredReason == preHitDecision.IgnoredReason;
            }

            public ResolvedHitRecord WithAmbiguousIdentity()
            {
                return new ResolvedHitRecord(
                    HitKey,
                    State,
                    Evidence,
                    SourcePolicy,
                    SourceEntityId,
                    TargetEntityId,
                    PolicyEventId,
                    LocalFrame,
                    PreHitDecision,
                    hasAmbiguousIdentity: true,
                    hasConsumedEvent: HasConsumedEvent);
            }

            public ResolvedHitRecord WithConsumedEvent()
            {
                return new ResolvedHitRecord(
                    HitKey,
                    State,
                    Evidence,
                    SourcePolicy,
                    SourceEntityId,
                    TargetEntityId,
                    PolicyEventId,
                    LocalFrame,
                    PreHitDecision,
                    HasAmbiguousIdentity,
                    hasConsumedEvent: true);
            }

            public bool Matches(
                in CharacterTestResolvedHitKey hitKey,
                in DamageProfileResolveResult profileResult)
            {
                DamageProfileUsage usage = profileResult.Usage;
                return !HasAmbiguousIdentity
                    && HitKey.Equals(hitKey)
                    && usage.SourceEntityId == SourceEntityId
                    && usage.TargetEntityId == TargetEntityId
                    && usage.ActionInstanceId == HitKey.ActionInstanceId
                    && usage.LocalFrame == LocalFrame
                    && profileResult.IncomingDamage == PreHitDecision.ResolvedDamage;
            }

            public bool Matches(in CharacterTestResolvedHitKey hitKey)
            {
                return !HasAmbiguousIdentity && HitKey.Equals(hitKey);
            }
        }

    }
}

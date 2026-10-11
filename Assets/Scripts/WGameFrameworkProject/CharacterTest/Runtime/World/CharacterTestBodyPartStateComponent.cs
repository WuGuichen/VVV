using System;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestBodyPartWoundState
    {
        Closed = 0,
        Open = 1,
        Consumed = 2
    }

    internal enum CharacterTestBodyPartStructureState
    {
        Intact = 0,
        Broken = 1,
        Severed = 2
    }

    internal enum CharacterTestBodyPartStateInitializationStatus
    {
        MissingDefinitions = 0,
        Ready = 1,
        InvalidBinding = 2
    }

    internal enum CharacterTestBodyPartEvidenceReason
    {
        ExplicitBinding = 0,
        NoDefinitions = 1,
        NoPartEvidence = 2,
        InvalidBinding = 3
    }

    internal enum CharacterTestBodyPartFilterStatus
    {
        Active = 0,
        Severed = 1,
        Untracked = 2,
        InvalidBinding = 3
    }

    internal enum CharacterTestBodyPartDependencyStatus
    {
        Satisfied = 0,
        Severed = 1,
        UnknownPartState = 2,
        InvalidBinding = 3
    }

    internal readonly struct CharacterTestBodyPartStateDefinition
    {
        public CharacterTestBodyPartStateDefinition(CharacterBodyPartDamageRuntimeBinding source)
        {
            DefinitionIndex = source.DefinitionIndex;
            PartStateId = source.PartStateId;
            DamageZoneId = source.DamageZoneId;
            WoundCapacity = source.WoundCapacity;
            OpenDamageScaleMilli = source.OpenDamageScaleMilli;
            FocusConsumeTag = source.FocusConsumeTag ?? string.Empty;
            MaxIntegrity = source.MaxIntegrity;
            IntegrityDamageScaleMilli = source.IntegrityDamageScaleMilli;
            BreakReactionId = source.BreakReactionId ?? string.Empty;
            AuthoredPriorityDeltaOpen = source.AuthoredPriorityDeltaOpen;
            AuthoredPriorityDeltaBroken = source.AuthoredPriorityDeltaBroken;
            Severable = source.Severable;
            SeverRequiredTag = source.SeverRequiredTag ?? string.Empty;
            PresentationEventId = source.PresentationEventId ?? string.Empty;
        }

        public int DefinitionIndex { get; }
        public int PartStateId { get; }
        public int DamageZoneId { get; }
        public int WoundCapacity { get; }
        public int OpenDamageScaleMilli { get; }
        public string FocusConsumeTag { get; }
        public int MaxIntegrity { get; }
        public int IntegrityDamageScaleMilli { get; }
        public string BreakReactionId { get; }
        public int AuthoredPriorityDeltaOpen { get; }
        public int AuthoredPriorityDeltaBroken { get; }
        public bool Severable { get; }
        public string SeverRequiredTag { get; }
        public string PresentationEventId { get; }
    }

    internal readonly struct CharacterTestBodyPartStateEntry
    {
        public CharacterTestBodyPartStateEntry(
            int definitionIndex,
            int woundValue,
            CharacterTestBodyPartWoundState woundState,
            int currentIntegrity,
            CharacterTestBodyPartStructureState structureState,
            long lastAcceptedHitFrame,
            long revision)
        {
            DefinitionIndex = definitionIndex;
            WoundValue = woundValue;
            WoundState = woundState;
            CurrentIntegrity = currentIntegrity;
            StructureState = structureState;
            LastAcceptedHitFrame = lastAcceptedHitFrame;
            Revision = revision;
        }

        public int DefinitionIndex { get; }
        public int WoundValue { get; }
        public CharacterTestBodyPartWoundState WoundState { get; }
        public int CurrentIntegrity { get; }
        public CharacterTestBodyPartStructureState StructureState { get; }
        public long LastAcceptedHitFrame { get; }
        public long Revision { get; }

        public CharacterTestBodyPartStateEntry WithAuthorityValues(
            int woundValue,
            CharacterTestBodyPartWoundState woundState,
            int currentIntegrity,
            CharacterTestBodyPartStructureState structureState,
            long lastAcceptedHitFrame)
        {
            return new CharacterTestBodyPartStateEntry(
                DefinitionIndex,
                woundValue,
                woundState,
                currentIntegrity,
                structureState,
                lastAcceptedHitFrame,
                Revision);
        }

        public CharacterTestBodyPartStateEntry WithRevision(long revision)
        {
            return new CharacterTestBodyPartStateEntry(
                DefinitionIndex,
                WoundValue,
                WoundState,
                CurrentIntegrity,
                StructureState,
                LastAcceptedHitFrame,
                revision);
        }
    }

    internal readonly struct CharacterTestBodyPartStateReadView
    {
        private readonly CharacterTestBodyPartStateEntry[] _entries;

        internal CharacterTestBodyPartStateReadView(
            CharacterTestBodyPartStateEntry[] entries,
            int count,
            long authorityFrame)
        {
            _entries = entries;
            Count = count;
            AuthorityFrame = authorityFrame;
        }

        public int Count { get; }
        public long AuthorityFrame { get; }

        public bool TryGet(int definitionIndex, out CharacterTestBodyPartStateEntry entry)
        {
            if (_entries != null
                && definitionIndex >= 0
                && definitionIndex < Count
                && _entries[definitionIndex].DefinitionIndex == definitionIndex)
            {
                entry = _entries[definitionIndex];
                return true;
            }

            entry = default;
            return false;
        }
    }

    /// <summary>
    /// CharacterTest-owned fixed-capacity authority for one entity's body-part state.
    /// </summary>
    /// <remarks>
    /// Initialization may allocate the compiled lookup and frame buffers. Frame begin,
    /// read, staging, and reduction are NoAlloc after initialization.
    /// </remarks>
    internal sealed partial class CharacterTestBodyPartStateComponent
    {
        internal const int MaximumDefinitionCount = 8;
        internal const int DefaultDamageFactCapacity = 128;
        internal const int DefaultFocusConsumeFactCapacity = 64;
        internal const int DefaultSeverFactCapacity = 64;

        private readonly CharacterTestBodyPartStateDefinition[] _definitions =
            new CharacterTestBodyPartStateDefinition[MaximumDefinitionCount];
        private readonly CharacterTestBodyPartStateEntry[] _entries =
            new CharacterTestBodyPartStateEntry[MaximumDefinitionCount];
        private readonly CharacterTestBodyPartStateEntry[] _frameStartEntries =
            new CharacterTestBodyPartStateEntry[MaximumDefinitionCount];
        private readonly CharacterTestBodyPartStateEntry[] _workEntries =
            new CharacterTestBodyPartStateEntry[MaximumDefinitionCount];
        private readonly int _damageFactCapacity;
        private readonly int _focusConsumeFactCapacity;
        private readonly int _severFactCapacity;
        private CharacterTestBodyPartLookupEntry[] _partLookup =
            Array.Empty<CharacterTestBodyPartLookupEntry>();

        internal CharacterTestBodyPartStateComponent(
            int ownerEntityId,
            int damageFactCapacity = DefaultDamageFactCapacity,
            int focusConsumeFactCapacity = DefaultFocusConsumeFactCapacity,
            int severFactCapacity = DefaultSeverFactCapacity)
        {
            OwnerEntityId = ownerEntityId;
            _damageFactCapacity = Math.Max(1, damageFactCapacity);
            _focusConsumeFactCapacity = Math.Max(1, focusConsumeFactCapacity);
            _severFactCapacity = Math.Max(1, severFactCapacity);
            InitializationStatus = CharacterTestBodyPartStateInitializationStatus.MissingDefinitions;
            DefinitionSourceFormat = string.Empty;
            DefinitionSourceSchemaVersion = string.Empty;
            DefinitionSourceContentHash = string.Empty;
        }

        public int OwnerEntityId { get; }
        public int DefinitionCount { get; private set; }
        public long Revision { get; private set; }
        public CharacterTestBodyPartStateInitializationStatus InitializationStatus { get; private set; }
        public string DefinitionSourceFormat { get; private set; }
        public string DefinitionSourceSchemaVersion { get; private set; }
        public int DefinitionSourceRevision { get; private set; }
        public string DefinitionSourceContentHash { get; private set; }
        private RuntimeHashDigest _sourceFormatDigest = RuntimeHashDigest.ComputeContentDigest(string.Empty);
        private RuntimeHashDigest _sourceVersionDigest = RuntimeHashDigest.ComputeContentDigest(string.Empty);
        private RuntimePreparedDigest _sourceContentDigest = new RuntimePreparedDigest(RuntimeDigestRepresentation.Empty, 0, default);

        internal CharacterTestBodyPartStateInitializationStatus Reinitialize(
            CharacterImportedGeometryBinding binding)
        {
            ResetAuthority();
            if (binding == null
                || binding.BodyPartDamageMode == CharacterBodyPartDamageMode.Missing)
            {
                return InitializationStatus;
            }

            CharacterBodyPartDamageRuntimeBinding[] sources = binding.BodyPartDamageDefinitions;
            CharacterBodyPartDamageLookupRuntimeBinding[] lookup = binding.BodyPartDamagePartLookup;
            if (binding.BodyPartDamageMode != CharacterBodyPartDamageMode.Authored
                || sources == null
                || sources.Length == 0
                || sources.Length > MaximumDefinitionCount
                || !ValidateDefinitions(sources)
                || !ValidateLookup(sources, lookup))
            {
                InitializationStatus = CharacterTestBodyPartStateInitializationStatus.InvalidBinding;
                return InitializationStatus;
            }

            DefinitionCount = sources.Length;
            for (int i = 0; i < DefinitionCount; i++)
            {
                CharacterBodyPartDamageRuntimeBinding source = sources[i];
                _definitions[i] = new CharacterTestBodyPartStateDefinition(source);
                _entries[i] = new CharacterTestBodyPartStateEntry(
                    i,
                    woundValue: 0,
                    CharacterTestBodyPartWoundState.Closed,
                    source.MaxIntegrity,
                    CharacterTestBodyPartStructureState.Intact,
                    lastAcceptedHitFrame: -1L,
                    revision: 0L);
            }

            _partLookup = new CharacterTestBodyPartLookupEntry[lookup?.Length ?? 0];
            for (int i = 0; i < _partLookup.Length; i++)
            {
                _partLookup[i] = new CharacterTestBodyPartLookupEntry(
                    lookup[i].PartId,
                    lookup[i].DefinitionIndex);
            }

            DefinitionSourceFormat = binding.BodyPartDamageFormat;
            DefinitionSourceSchemaVersion = binding.BodyPartDamageSchemaVersion;
            DefinitionSourceRevision = binding.BodyPartDamageRevision;
            DefinitionSourceContentHash = binding.BodyPartDamageContentHash;
            _sourceFormatDigest = RuntimeHashDigest.ComputeContentDigest(DefinitionSourceFormat);
            _sourceVersionDigest = RuntimeHashDigest.ComputeContentDigest(DefinitionSourceSchemaVersion);
            _sourceContentDigest = RuntimePreparedDigest.PrepareExisting(DefinitionSourceContentHash);
            InitializationStatus = CharacterTestBodyPartStateInitializationStatus.Ready;
            EnsureReducerBuffers();
            return InitializationStatus;
        }

        internal bool TryGetDefinition(
            int definitionIndex,
            out CharacterTestBodyPartStateDefinition definition)
        {
            if (definitionIndex >= 0
                && definitionIndex < DefinitionCount
                && _definitions[definitionIndex].DefinitionIndex == definitionIndex)
            {
                definition = _definitions[definitionIndex];
                return true;
            }

            definition = default;
            return false;
        }

        internal bool TryGetCurrentEntry(
            int definitionIndex,
            out CharacterTestBodyPartStateEntry entry)
        {
            if (definitionIndex >= 0
                && definitionIndex < DefinitionCount
                && _entries[definitionIndex].DefinitionIndex == definitionIndex)
            {
                entry = _entries[definitionIndex];
                return true;
            }

            entry = default;
            return false;
        }

        /// <summary>
        /// Bounded, allocation-free boundary lookup. Reducer facts carry the returned numeric index.
        /// </summary>
        internal bool TryResolveDefinitionIndex(
            int bodyPartId,
            out int definitionIndex,
            out CharacterTestBodyPartEvidenceReason reason)
        {
            definitionIndex = -1;
            if (InitializationStatus == CharacterTestBodyPartStateInitializationStatus.InvalidBinding)
            {
                reason = CharacterTestBodyPartEvidenceReason.InvalidBinding;
                return false;
            }
            if (DefinitionCount == 0)
            {
                reason = CharacterTestBodyPartEvidenceReason.NoDefinitions;
                return false;
            }
            if ((bodyPartId <= 0))
            {
                reason = CharacterTestBodyPartEvidenceReason.NoPartEvidence;
                return false;
            }

            int low = 0;
            int high = _partLookup.Length - 1;
            while (low <= high)
            {
                int middle = low + ((high - low) >> 1);
                CharacterTestBodyPartLookupEntry candidate = _partLookup[middle];
                int compare = candidate.PartId.CompareTo(bodyPartId);
                if (compare == 0)
                {
                    definitionIndex = candidate.DefinitionIndex;
                    reason = CharacterTestBodyPartEvidenceReason.ExplicitBinding;
                    return true;
                }

                if (compare < 0)
                    low = middle + 1;
                else
                    high = middle - 1;
            }

            reason = CharacterTestBodyPartEvidenceReason.NoPartEvidence;
            return false;
        }

        internal bool TryResolveDefinitionIndexByPartStateId(
            int partStateId,
            out int definitionIndex)
        {
            definitionIndex = -1;
            if (InitializationStatus != CharacterTestBodyPartStateInitializationStatus.Ready
                || (partStateId <= 0))
            {
                return false;
            }

            int low = 0;
            int high = DefinitionCount - 1;
            while (low <= high)
            {
                int middle = low + ((high - low) >> 1);
                int compare = ComparePartStateOrder(_definitions[middle].PartStateId, partStateId);
                if (compare == 0)
                {
                    definitionIndex = middle;
                    return true;
                }
                if (compare < 0)
                    low = middle + 1;
                else
                    high = middle - 1;
            }
            return false;
        }

        /// <summary>Stable read-only filter seam for Hurtbox topology consumers.</summary>
        internal CharacterTestBodyPartFilterStatus GetBodyPartFilterStatus(int bodyPartId)
        {
            if (InitializationStatus == CharacterTestBodyPartStateInitializationStatus.InvalidBinding)
                return CharacterTestBodyPartFilterStatus.InvalidBinding;
            if (!TryResolveDefinitionIndex(bodyPartId, out int definitionIndex, out _))
                return CharacterTestBodyPartFilterStatus.Untracked;
            return _entries[definitionIndex].StructureState == CharacterTestBodyPartStructureState.Severed
                ? CharacterTestBodyPartFilterStatus.Severed
                : CharacterTestBodyPartFilterStatus.Active;
        }

        /// <summary>Stable read-only dependency seam; action events reference partStateId directly.</summary>
        internal CharacterTestBodyPartDependencyStatus GetPartStateDependencyStatus(
            int partStateId)
        {
            if (InitializationStatus == CharacterTestBodyPartStateInitializationStatus.InvalidBinding)
                return CharacterTestBodyPartDependencyStatus.InvalidBinding;
            if (!TryResolveDefinitionIndexByPartStateId(partStateId, out int definitionIndex))
                return CharacterTestBodyPartDependencyStatus.UnknownPartState;
            return _entries[definitionIndex].StructureState == CharacterTestBodyPartStructureState.Severed
                ? CharacterTestBodyPartDependencyStatus.Severed
                : CharacterTestBodyPartDependencyStatus.Satisfied;
        }

        private void ResetAuthority()
        {
            Array.Clear(_definitions, 0, _definitions.Length);
            Array.Clear(_entries, 0, _entries.Length);
            Array.Clear(_frameStartEntries, 0, _frameStartEntries.Length);
            Array.Clear(_workEntries, 0, _workEntries.Length);
            _partLookup = Array.Empty<CharacterTestBodyPartLookupEntry>();
            DefinitionCount = 0;
            Revision = 0L;
            DefinitionSourceFormat = string.Empty;
            DefinitionSourceSchemaVersion = string.Empty;
            DefinitionSourceRevision = 0;
            DefinitionSourceContentHash = string.Empty;
            _sourceFormatDigest = RuntimeHashDigest.ComputeContentDigest(string.Empty);
            _sourceVersionDigest = RuntimeHashDigest.ComputeContentDigest(string.Empty);
            _sourceContentDigest = new RuntimePreparedDigest(RuntimeDigestRepresentation.Empty, 0, default);
            InitializationStatus = CharacterTestBodyPartStateInitializationStatus.MissingDefinitions;
            ResetReducerState();
        }

        private static int ComparePartStateOrder(int left, int right) =>
            CharacterTestAuthoredContentIds.GetLegacyOrdinalRank(400, left)
                .CompareTo(CharacterTestAuthoredContentIds.GetLegacyOrdinalRank(400, right));

        private static bool ValidateDefinitions(CharacterBodyPartDamageRuntimeBinding[] definitions)
        {
            int previousPartStateId = 0;
            for (int i = 0; i < definitions.Length; i++)
            {
                CharacterBodyPartDamageRuntimeBinding definition = definitions[i];
                if (definition == null
                    || definition.DefinitionIndex != i
                    || (definition.PartStateId <= 0)
                    || (i > 0 && ComparePartStateOrder(previousPartStateId, definition.PartStateId) >= 0)
                    || definition.MemberPartIds == null
                    || definition.MemberPartIds.Length == 0
                    || definition.WoundCapacity < 0
                    || definition.WoundCapacity > 1_000_000
                    || definition.OpenDamageScaleMilli < 0
                    || definition.OpenDamageScaleMilli > 4_000
                    || (definition.WoundCapacity > 0 && definition.OpenDamageScaleMilli == 0)
                    || definition.MaxIntegrity < 0
                    || definition.MaxIntegrity > 1_000_000
                    || definition.IntegrityDamageScaleMilli < 0
                    || definition.IntegrityDamageScaleMilli > 4_000
                    || definition.AuthoredPriorityDeltaOpen < -1_000
                    || definition.AuthoredPriorityDeltaOpen > 1_000
                    || definition.AuthoredPriorityDeltaBroken < -1_000
                    || definition.AuthoredPriorityDeltaBroken > 1_000
                    || (!definition.Severable
                        && (!string.IsNullOrEmpty(definition.SeverRequiredTag)
                            || !string.IsNullOrEmpty(definition.PresentationEventId)))
                    || (definition.Severable
                        && (definition.MaxIntegrity == 0
                            || !IsExplicitAuthorityTag(definition.SeverRequiredTag)
                            || !IsStableIdentity(definition.PresentationEventId)
                            || !definition.PresentationEventId.StartsWith(
                                "event.",
                                StringComparison.Ordinal))))
                {
                    return false;
                }

                if (definition.Severable)
                {
                    for (int previousIndex = 0; previousIndex < i; previousIndex++)
                    {
                        CharacterBodyPartDamageRuntimeBinding previous = definitions[previousIndex];
                        if (previous != null
                            && previous.Severable
                            && string.Equals(
                                previous.PresentationEventId,
                                definition.PresentationEventId,
                                StringComparison.Ordinal))
                        {
                            return false;
                        }
                    }
                }

                previousPartStateId = definition.PartStateId;
            }

            return true;
        }

        private static bool ValidateLookup(
            CharacterBodyPartDamageRuntimeBinding[] definitions,
            CharacterBodyPartDamageLookupRuntimeBinding[] lookup)
        {
            int expectedCount = 0;
            for (int i = 0; i < definitions.Length; i++)
                expectedCount += definitions[i].MemberPartIds.Length;
            if ((lookup?.Length ?? 0) != expectedCount)
                return false;

            int previousPartId = 0;
            for (int i = 0; i < lookup.Length; i++)
            {
                CharacterBodyPartDamageLookupRuntimeBinding entry = lookup[i];
                if (entry == null
                    || (entry.PartId <= 0)
                    || (i > 0 && previousPartId.CompareTo(entry.PartId) >= 0)
                    || entry.DefinitionIndex < 0
                    || entry.DefinitionIndex >= definitions.Length)
                {
                    return false;
                }

                bool declared = false;
                int[] members = definitions[entry.DefinitionIndex].MemberPartIds;
                for (int memberIndex = 0; memberIndex < members.Length; memberIndex++)
                {
                    if (members[memberIndex] == entry.PartId)
                    {
                        declared = true;
                        break;
                    }
                }
                if (!declared)
                    return false;
                previousPartId = entry.PartId;
            }

            return true;
        }

        private static bool IsExplicitAuthorityTag(string value)
        {
            return IsStableIdentity(value)
                && (value.StartsWith("action.", StringComparison.Ordinal)
                    || value.StartsWith("ability.", StringComparison.Ordinal)
                    || value.StartsWith("interaction.", StringComparison.Ordinal));
        }

        private static bool IsStableIdentity(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 96)
                return false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (!char.IsLower(c) && !char.IsDigit(c) && c != '.' && c != '_' && c != '-')
                    return false;
            }
            return true;
        }

        private readonly struct CharacterTestBodyPartLookupEntry
        {
            public CharacterTestBodyPartLookupEntry(int partId, int definitionIndex)
            {
                PartId = partId;
                DefinitionIndex = definitionIndex;
            }

            public int PartId { get; }
            public int DefinitionIndex { get; }
        }
    }
}

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestBodyPartStateStageResult
    {
        Staged = 0,
        FrameNotOpen = 1,
        FrameMismatch = 2,
        RejectedInput = 3,
        NoDefinitions = 4,
        InvalidBinding = 5,
        InvalidDefinitionIndex = 6,
        CapacityOverflow = 7
    }

    internal enum CharacterTestBodyPartStateCommitStatus
    {
        Committed = 0,
        NoChanges = 1,
        FrameNotOpen = 2,
        CapacityOverflow = 3,
        ConflictingDuplicate = 4,
        RevisionExhausted = 5
    }

    internal enum CharacterTestBodyPartFocusConsumeAdapterKind
    {
        None = 0,
        Action = 1,
        Ability = 2,
        Interaction = 3
    }

    internal enum CharacterTestBodyPartTransitionAxis
    {
        Wound = 0,
        Structure = 1
    }

    internal enum CharacterTestBodyPartTransitionReason
    {
        Opened = 0,
        Broken = 1,
        Consumed = 2,
        Severed = 3
    }

    internal readonly struct CharacterTestBodyPartAcceptedDamageFact
    {
        public CharacterTestBodyPartAcceptedDamageFact(
            long authorityFrame,
            int sourceEntityId,
            int targetEntityId,
            int actionInstanceId,
            int policyEventId,
            int traceId,
            int localFrame,
            int definitionIndex,
            int finalDamage,
            bool authorityAccepted = true)
        {
            AuthorityFrame = authorityFrame;
            SourceEntityId = sourceEntityId;
            TargetEntityId = targetEntityId;
            ActionInstanceId = actionInstanceId;
            PolicyEventId = policyEventId;
            TraceId = traceId;
            LocalFrame = localFrame;
            DefinitionIndex = definitionIndex;
            FinalDamage = finalDamage;
            AuthorityAccepted = authorityAccepted;
        }

        public long AuthorityFrame { get; }
        public int SourceEntityId { get; }
        public int TargetEntityId { get; }
        public int ActionInstanceId { get; }
        public int PolicyEventId { get; }
        public int TraceId { get; }
        public int LocalFrame { get; }
        public int DefinitionIndex { get; }
        public int FinalDamage { get; }
        public bool AuthorityAccepted { get; }
    }

    internal readonly struct CharacterTestBodyPartFocusConsumeFact
    {
        public CharacterTestBodyPartFocusConsumeFact(
            long authorityFrame,
            CharacterTestBodyPartFocusConsumeAdapterKind adapterKind,
            int sourceEntityId,
            int targetEntityId,
            int definitionIndex,
            string focusConsumeTag,
            long correlationId,
            bool authorityAccepted = true)
        {
            AuthorityFrame = authorityFrame;
            AdapterKind = adapterKind;
            SourceEntityId = sourceEntityId;
            TargetEntityId = targetEntityId;
            DefinitionIndex = definitionIndex;
            FocusConsumeTag = focusConsumeTag ?? string.Empty;
            CorrelationId = correlationId;
            AuthorityAccepted = authorityAccepted;
        }

        public long AuthorityFrame { get; }
        public CharacterTestBodyPartFocusConsumeAdapterKind AdapterKind { get; }
        public int SourceEntityId { get; }
        public int TargetEntityId { get; }
        public int DefinitionIndex { get; }
        public string FocusConsumeTag { get; }
        public long CorrelationId { get; }
        public bool AuthorityAccepted { get; }
    }

    internal readonly struct CharacterTestBodyPartSeverFact
    {
        public CharacterTestBodyPartSeverFact(
            long authorityFrame,
            int sourceEntityId,
            int targetEntityId,
            int definitionIndex,
            string severTag,
            long correlationId,
            bool authorityAccepted = true)
        {
            AuthorityFrame = authorityFrame;
            SourceEntityId = sourceEntityId;
            TargetEntityId = targetEntityId;
            DefinitionIndex = definitionIndex;
            SeverTag = severTag ?? string.Empty;
            CorrelationId = correlationId;
            AuthorityAccepted = authorityAccepted;
        }

        public long AuthorityFrame { get; }
        public int SourceEntityId { get; }
        public int TargetEntityId { get; }
        public int DefinitionIndex { get; }
        public string SeverTag { get; }
        public long CorrelationId { get; }
        public bool AuthorityAccepted { get; }
    }

    internal readonly struct CharacterTestBodyPartStateTransition
    {
        internal CharacterTestBodyPartStateTransition(
            long authorityFrame,
            int sourceEntityId,
            int targetEntityId,
            CharacterTestBodyPartStateDefinition definition,
            CharacterTestBodyPartTransitionAxis axis,
            CharacterTestBodyPartTransitionReason reason,
            CharacterTestBodyPartStateEntry before,
            CharacterTestBodyPartStateEntry after,
            int appliedDamage,
            int actionInstanceId,
            int policyEventId,
            int traceId,
            int localFrame,
            CharacterTestBodyPartFocusConsumeAdapterKind focusAdapterKind,
            long focusCorrelationId)
        {
            AuthorityFrame = authorityFrame;
            SourceEntityId = sourceEntityId;
            TargetEntityId = targetEntityId;
            DefinitionIndex = definition.DefinitionIndex;
            PartStateId = definition.PartStateId;
            DamageZoneId = definition.DamageZoneId;
            Axis = axis;
            Reason = reason;
            Before = before;
            After = after;
            AppliedDamage = appliedDamage;
            ActionInstanceId = actionInstanceId;
            PolicyEventId = policyEventId;
            TraceId = traceId;
            LocalFrame = localFrame;
            FocusAdapterKind = focusAdapterKind;
            FocusCorrelationId = focusCorrelationId;
        }

        public long AuthorityFrame { get; }
        public int SourceEntityId { get; }
        public int TargetEntityId { get; }
        public int DefinitionIndex { get; }
        public int PartStateId { get; }
        public int DamageZoneId { get; }
        public CharacterTestBodyPartTransitionAxis Axis { get; }
        public CharacterTestBodyPartTransitionReason Reason { get; }
        public CharacterTestBodyPartStateEntry Before { get; }
        public CharacterTestBodyPartStateEntry After { get; }
        public int AppliedDamage { get; }
        public int ActionInstanceId { get; }
        public int PolicyEventId { get; }
        public int TraceId { get; }
        public int LocalFrame { get; }
        public CharacterTestBodyPartFocusConsumeAdapterKind FocusAdapterKind { get; }
        public long FocusCorrelationId { get; }
    }

    internal readonly struct CharacterTestBodyPartStateCommitResult
    {
        public CharacterTestBodyPartStateCommitResult(
            CharacterTestBodyPartStateCommitStatus status,
            int acceptedDamageFactCount,
            int focusConsumeFactCount,
            int transitionCount,
            int severFactCount = 0)
        {
            Status = status;
            AcceptedDamageFactCount = acceptedDamageFactCount;
            FocusConsumeFactCount = focusConsumeFactCount;
            TransitionCount = transitionCount;
            SeverFactCount = severFactCount;
        }

        public CharacterTestBodyPartStateCommitStatus Status { get; }
        public int AcceptedDamageFactCount { get; }
        public int FocusConsumeFactCount { get; }
        public int TransitionCount { get; }
        public int SeverFactCount { get; }
        public bool Succeeded =>
            Status == CharacterTestBodyPartStateCommitStatus.Committed
            || Status == CharacterTestBodyPartStateCommitStatus.NoChanges;
    }

    internal sealed partial class CharacterTestBodyPartStateComponent
    {
        private const int MaximumTransitionCount = MaximumDefinitionCount * 3;

        private CharacterTestBodyPartAcceptedDamageFact[] _damageFacts =
            Array.Empty<CharacterTestBodyPartAcceptedDamageFact>();
        private CharacterTestBodyPartFocusConsumeFact[] _focusConsumeFacts =
            Array.Empty<CharacterTestBodyPartFocusConsumeFact>();
        private CharacterTestBodyPartSeverFact[] _severFacts =
            Array.Empty<CharacterTestBodyPartSeverFact>();
        private CharacterTestBodyPartStateTransition[] _transitions =
            Array.Empty<CharacterTestBodyPartStateTransition>();
        private readonly bool[] _touchedEntries = new bool[MaximumDefinitionCount];
        private int _damageFactCount;
        private int _focusConsumeFactCount;
        private int _severFactCount;
        private int _transitionCount;
        private bool _damageOverflowed;
        private bool _focusConsumeOverflowed;
        private bool _severOverflowed;
        private bool _revisionExhausted;
        private bool _frameOpen;
        private long _activeFrame = -1L;
        private CharacterTestBodyPartStateReadView _frameStartView;

        public int TransitionCount => _transitionCount;
        public long TotalDamageOverflowCount { get; private set; }
        public long TotalFocusConsumeOverflowCount { get; private set; }
        public long TotalSeverOverflowCount { get; private set; }

        internal bool TryBeginFrame(
            long authorityFrame,
            out CharacterTestBodyPartStateReadView readView)
        {
            if (authorityFrame < 0L || _frameOpen)
            {
                readView = default;
                return false;
            }

            for (int i = 0; i < DefinitionCount; i++)
                _frameStartEntries[i] = _entries[i];

            _activeFrame = authorityFrame;
            _damageFactCount = 0;
            _focusConsumeFactCount = 0;
            _severFactCount = 0;
            _transitionCount = 0;
            _damageOverflowed = false;
            _focusConsumeOverflowed = false;
            _severOverflowed = false;
            _revisionExhausted = false;
            _frameOpen = true;
            _frameStartView = new CharacterTestBodyPartStateReadView(
                _frameStartEntries,
                DefinitionCount,
                authorityFrame);
            readView = _frameStartView;
            return true;
        }

        internal CharacterTestBodyPartStateStageResult StageAcceptedDamage(
            in CharacterTestBodyPartAcceptedDamageFact fact)
        {
            CharacterTestBodyPartStateStageResult validation = ValidateStage(
                fact.AuthorityFrame,
                fact.TargetEntityId,
                fact.DefinitionIndex);
            if (validation != CharacterTestBodyPartStateStageResult.Staged)
                return validation;
            if (!fact.AuthorityAccepted
                || fact.FinalDamage <= 0
                || fact.SourceEntityId <= 0
                || fact.ActionInstanceId < 0
                || fact.PolicyEventId < 0
                || fact.TraceId < 0
                || fact.LocalFrame < 0)
            {
                return CharacterTestBodyPartStateStageResult.RejectedInput;
            }
            if (_damageFactCount >= _damageFacts.Length)
            {
                _damageOverflowed = true;
                TotalDamageOverflowCount = SaturatingIncrement(TotalDamageOverflowCount);
                return CharacterTestBodyPartStateStageResult.CapacityOverflow;
            }

            _damageFacts[_damageFactCount++] = fact;
            return CharacterTestBodyPartStateStageResult.Staged;
        }

        internal CharacterTestBodyPartStateStageResult StageFocusConsume(
            in CharacterTestBodyPartFocusConsumeFact fact)
        {
            CharacterTestBodyPartStateStageResult validation = ValidateStage(
                fact.AuthorityFrame,
                fact.TargetEntityId,
                fact.DefinitionIndex);
            if (validation != CharacterTestBodyPartStateStageResult.Staged)
                return validation;
            if (!fact.AuthorityAccepted
                || fact.SourceEntityId <= 0
                || fact.AdapterKind == CharacterTestBodyPartFocusConsumeAdapterKind.None
                || fact.CorrelationId <= 0L
                || string.IsNullOrEmpty(fact.FocusConsumeTag))
            {
                return CharacterTestBodyPartStateStageResult.RejectedInput;
            }
            if (_focusConsumeFactCount >= _focusConsumeFacts.Length)
            {
                _focusConsumeOverflowed = true;
                TotalFocusConsumeOverflowCount = SaturatingIncrement(
                    TotalFocusConsumeOverflowCount);
                return CharacterTestBodyPartStateStageResult.CapacityOverflow;
            }

            _focusConsumeFacts[_focusConsumeFactCount++] = fact;
            return CharacterTestBodyPartStateStageResult.Staged;
        }

        internal CharacterTestBodyPartStateStageResult StageSever(
            in CharacterTestBodyPartSeverFact fact)
        {
            CharacterTestBodyPartStateStageResult validation = ValidateStage(
                fact.AuthorityFrame,
                fact.TargetEntityId,
                fact.DefinitionIndex);
            if (validation != CharacterTestBodyPartStateStageResult.Staged)
                return validation;
            if (!fact.AuthorityAccepted
                || fact.SourceEntityId <= 0
                || fact.SourceEntityId == fact.TargetEntityId
                || fact.CorrelationId <= 0L
                || string.IsNullOrEmpty(fact.SeverTag))
            {
                return CharacterTestBodyPartStateStageResult.RejectedInput;
            }
            if (_severFactCount >= _severFacts.Length)
            {
                _severOverflowed = true;
                TotalSeverOverflowCount = SaturatingIncrement(TotalSeverOverflowCount);
                return CharacterTestBodyPartStateStageResult.CapacityOverflow;
            }

            _severFacts[_severFactCount++] = fact;
            return CharacterTestBodyPartStateStageResult.Staged;
        }

        internal CharacterTestBodyPartStateCommitResult CommitFrame()
        {
            if (!_frameOpen)
            {
                return new CharacterTestBodyPartStateCommitResult(
                    CharacterTestBodyPartStateCommitStatus.FrameNotOpen,
                    0,
                    0,
                    0);
            }
            if (_damageOverflowed || _focusConsumeOverflowed || _severOverflowed)
                return FinishFailedCommit(CharacterTestBodyPartStateCommitStatus.CapacityOverflow);

            SortDamageFacts();
            SortFocusConsumeFacts();
            SortSeverFacts();
            for (int i = 0; i < DefinitionCount; i++)
            {
                _workEntries[i] = _entries[i];
                _touchedEntries[i] = false;
            }
            _transitionCount = 0;

            int acceptedDamageFactCount = 0;
            for (int i = 0; i < _damageFactCount; i++)
            {
                CharacterTestBodyPartAcceptedDamageFact fact = _damageFacts[i];
                if (i > 0 && SameDamageKey(_damageFacts[i - 1], fact))
                {
                    if (_damageFacts[i - 1].FinalDamage != fact.FinalDamage)
                    {
                        return FinishFailedCommit(
                            CharacterTestBodyPartStateCommitStatus.ConflictingDuplicate);
                    }
                    continue;
                }

                acceptedDamageFactCount++;
                if (!ApplyDamageFact(fact))
                {
                    return FinishFailedCommit(
                        _revisionExhausted
                            ? CharacterTestBodyPartStateCommitStatus.RevisionExhausted
                            : CharacterTestBodyPartStateCommitStatus.CapacityOverflow);
                }
            }

            int focusConsumeFactCount = 0;
            for (int i = 0; i < _focusConsumeFactCount; i++)
            {
                CharacterTestBodyPartFocusConsumeFact fact = _focusConsumeFacts[i];
                if (i > 0 && SameFocusKey(_focusConsumeFacts[i - 1], fact))
                {
                    if (!string.Equals(
                            _focusConsumeFacts[i - 1].FocusConsumeTag,
                            fact.FocusConsumeTag,
                            StringComparison.Ordinal))
                    {
                        return FinishFailedCommit(
                            CharacterTestBodyPartStateCommitStatus.ConflictingDuplicate);
                    }
                    continue;
                }

                if (ApplyFocusConsumeFact(fact, out bool consumed))
                {
                    if (consumed)
                        focusConsumeFactCount++;
                }
                else
                {
                    return FinishFailedCommit(
                        _revisionExhausted
                            ? CharacterTestBodyPartStateCommitStatus.RevisionExhausted
                            : CharacterTestBodyPartStateCommitStatus.CapacityOverflow);
                }
            }

            int severFactCount = 0;
            for (int i = 0; i < _severFactCount; i++)
            {
                CharacterTestBodyPartSeverFact fact = _severFacts[i];
                if (i > 0 && SameSeverKey(_severFacts[i - 1], fact))
                {
                    if (!string.Equals(
                            _severFacts[i - 1].SeverTag,
                            fact.SeverTag,
                            StringComparison.Ordinal))
                    {
                        return FinishFailedCommit(
                            CharacterTestBodyPartStateCommitStatus.ConflictingDuplicate);
                    }
                    continue;
                }

                if (ApplySeverFact(fact, out bool severed))
                {
                    if (severed)
                        severFactCount++;
                }
                else
                {
                    return FinishFailedCommit(
                        _revisionExhausted
                            ? CharacterTestBodyPartStateCommitStatus.RevisionExhausted
                            : CharacterTestBodyPartStateCommitStatus.CapacityOverflow);
                }
            }

            int touchedCount = 0;
            for (int i = 0; i < DefinitionCount; i++)
            {
                if (_touchedEntries[i])
                {
                    touchedCount++;
                    if (_workEntries[i].Revision == _entries[i].Revision)
                    {
                        if (_workEntries[i].Revision == long.MaxValue)
                        {
                            return FinishFailedCommit(
                                CharacterTestBodyPartStateCommitStatus.RevisionExhausted);
                        }

                        _workEntries[i] = _workEntries[i].WithRevision(
                            _workEntries[i].Revision + 1L);
                    }
                }
            }

            if (touchedCount == 0)
            {
                return FinishSuccessfulCommit(
                    CharacterTestBodyPartStateCommitStatus.NoChanges,
                    acceptedDamageFactCount,
                    focusConsumeFactCount,
                    severFactCount);
            }
            if (Revision == long.MaxValue)
            {
                return FinishFailedCommit(
                    CharacterTestBodyPartStateCommitStatus.RevisionExhausted);
            }

            for (int i = 0; i < DefinitionCount; i++)
                _entries[i] = _workEntries[i];
            Revision++;

            return FinishSuccessfulCommit(
                CharacterTestBodyPartStateCommitStatus.Committed,
                acceptedDamageFactCount,
                focusConsumeFactCount,
                severFactCount);
        }

        internal CharacterTestBodyPartStateTransition GetTransitionAt(int index)
        {
            return index >= 0 && index < _transitionCount
                ? _transitions[index]
                : default;
        }

        private void EnsureReducerBuffers()
        {
            if (_damageFacts.Length != _damageFactCapacity)
                _damageFacts = new CharacterTestBodyPartAcceptedDamageFact[_damageFactCapacity];
            if (_focusConsumeFacts.Length != _focusConsumeFactCapacity)
                _focusConsumeFacts = new CharacterTestBodyPartFocusConsumeFact[_focusConsumeFactCapacity];
            if (_severFacts.Length != _severFactCapacity)
                _severFacts = new CharacterTestBodyPartSeverFact[_severFactCapacity];
            if (_transitions.Length != MaximumTransitionCount)
                _transitions = new CharacterTestBodyPartStateTransition[MaximumTransitionCount];
        }

        private void ResetReducerState()
        {
            if (_damageFacts.Length > 0)
                Array.Clear(_damageFacts, 0, _damageFacts.Length);
            if (_focusConsumeFacts.Length > 0)
                Array.Clear(_focusConsumeFacts, 0, _focusConsumeFacts.Length);
            if (_severFacts.Length > 0)
                Array.Clear(_severFacts, 0, _severFacts.Length);
            if (_transitions.Length > 0)
                Array.Clear(_transitions, 0, _transitions.Length);
            Array.Clear(_touchedEntries, 0, _touchedEntries.Length);
            _damageFactCount = 0;
            _focusConsumeFactCount = 0;
            _severFactCount = 0;
            _transitionCount = 0;
            _damageOverflowed = false;
            _focusConsumeOverflowed = false;
            _severOverflowed = false;
            _revisionExhausted = false;
            _frameOpen = false;
            _activeFrame = -1L;
            _frameStartView = default;
            TotalDamageOverflowCount = 0L;
            TotalFocusConsumeOverflowCount = 0L;
            TotalSeverOverflowCount = 0L;
        }

        private CharacterTestBodyPartStateStageResult ValidateStage(
            long authorityFrame,
            int targetEntityId,
            int definitionIndex)
        {
            if (!_frameOpen)
                return CharacterTestBodyPartStateStageResult.FrameNotOpen;
            if (authorityFrame != _activeFrame)
                return CharacterTestBodyPartStateStageResult.FrameMismatch;
            if (InitializationStatus == CharacterTestBodyPartStateInitializationStatus.InvalidBinding)
                return CharacterTestBodyPartStateStageResult.InvalidBinding;
            if (DefinitionCount == 0)
                return CharacterTestBodyPartStateStageResult.NoDefinitions;
            if (targetEntityId != OwnerEntityId || definitionIndex < 0 || definitionIndex >= DefinitionCount)
                return CharacterTestBodyPartStateStageResult.InvalidDefinitionIndex;
            return CharacterTestBodyPartStateStageResult.Staged;
        }

        private bool ApplyDamageFact(in CharacterTestBodyPartAcceptedDamageFact fact)
        {
            int definitionIndex = fact.DefinitionIndex;
            CharacterTestBodyPartStateDefinition definition = _definitions[definitionIndex];
            CharacterTestBodyPartStateEntry before = _workEntries[definitionIndex];
            int woundValue = before.WoundValue;
            int currentIntegrity = before.CurrentIntegrity;

            if (definition.WoundCapacity > 0
                && before.WoundState != CharacterTestBodyPartWoundState.Consumed)
            {
                woundValue = SaturatingAddToCapacity(
                    woundValue,
                    fact.FinalDamage,
                    definition.WoundCapacity);
            }
            bool opened = before.WoundState == CharacterTestBodyPartWoundState.Closed
                && definition.WoundCapacity > 0
                && woundValue >= definition.WoundCapacity;

            if (definition.MaxIntegrity > 0
                && before.StructureState == CharacterTestBodyPartStructureState.Intact)
            {
                long scaled = (long)fact.FinalDamage * definition.IntegrityDamageScaleMilli;
                int integrityDamage = (int)Math.Min(int.MaxValue, scaled / 1_000L);
                currentIntegrity = Math.Max(0, currentIntegrity - integrityDamage);
            }
            bool broken = before.StructureState == CharacterTestBodyPartStructureState.Intact
                && definition.MaxIntegrity > 0
                && currentIntegrity == 0;

            CharacterTestBodyPartStateEntry after = before.WithAuthorityValues(
                woundValue,
                before.WoundState,
                currentIntegrity,
                before.StructureState,
                fact.AuthorityFrame);
            if (opened)
            {
                CharacterTestBodyPartStateEntry transitionBefore = after;
                after = after.WithAuthorityValues(
                    after.WoundValue,
                    CharacterTestBodyPartWoundState.Open,
                    after.CurrentIntegrity,
                    after.StructureState,
                    after.LastAcceptedHitFrame);
                if (!TryAdvanceEntryRevision(after, out after)
                    || !TryAppendDamageTransition(
                        definition,
                        CharacterTestBodyPartTransitionAxis.Wound,
                        CharacterTestBodyPartTransitionReason.Opened,
                        transitionBefore,
                        after,
                        fact))
                {
                    return false;
                }
            }
            if (broken)
            {
                CharacterTestBodyPartStateEntry transitionBefore = after;
                after = after.WithAuthorityValues(
                    after.WoundValue,
                    after.WoundState,
                    after.CurrentIntegrity,
                    CharacterTestBodyPartStructureState.Broken,
                    after.LastAcceptedHitFrame);
                if (!TryAdvanceEntryRevision(after, out after)
                    || !TryAppendDamageTransition(
                        definition,
                        CharacterTestBodyPartTransitionAxis.Structure,
                        CharacterTestBodyPartTransitionReason.Broken,
                        transitionBefore,
                        after,
                        fact))
                {
                    return false;
                }
            }

            if (!SameEntry(before, after))
            {
                _workEntries[definitionIndex] = after;
                _touchedEntries[definitionIndex] = true;
            }
            return true;
        }

        private bool ApplyFocusConsumeFact(
            in CharacterTestBodyPartFocusConsumeFact fact,
            out bool consumed)
        {
            consumed = false;
            int definitionIndex = fact.DefinitionIndex;
            CharacterTestBodyPartStateDefinition definition = _definitions[definitionIndex];
            if (_frameStartEntries[definitionIndex].WoundState
                    != CharacterTestBodyPartWoundState.Open
                || string.IsNullOrEmpty(definition.FocusConsumeTag)
                || !string.Equals(
                    definition.FocusConsumeTag,
                    fact.FocusConsumeTag,
                    StringComparison.Ordinal))
            {
                return true;
            }

            CharacterTestBodyPartStateEntry before = _workEntries[definitionIndex];
            if (before.WoundState != CharacterTestBodyPartWoundState.Open)
                return true;
            CharacterTestBodyPartStateEntry after = before.WithAuthorityValues(
                before.WoundValue,
                CharacterTestBodyPartWoundState.Consumed,
                before.CurrentIntegrity,
                before.StructureState,
                before.LastAcceptedHitFrame);
            if (!TryAdvanceEntryRevision(after, out after)
                || !TryAppendFocusTransition(definition, before, after, fact))
            {
                return false;
            }

            _workEntries[definitionIndex] = after;
            _touchedEntries[definitionIndex] = true;
            consumed = true;
            return true;
        }

        private bool ApplySeverFact(
            in CharacterTestBodyPartSeverFact fact,
            out bool severed)
        {
            severed = false;
            int definitionIndex = fact.DefinitionIndex;
            CharacterTestBodyPartStateDefinition definition = _definitions[definitionIndex];
            if (!definition.Severable
                || _frameStartEntries[definitionIndex].StructureState
                    != CharacterTestBodyPartStructureState.Broken
                || !string.Equals(
                    definition.SeverRequiredTag,
                    fact.SeverTag,
                    StringComparison.Ordinal))
            {
                return true;
            }

            CharacterTestBodyPartStateEntry before = _workEntries[definitionIndex];
            if (before.StructureState != CharacterTestBodyPartStructureState.Broken)
                return true;
            CharacterTestBodyPartStateEntry after = before.WithAuthorityValues(
                before.WoundValue,
                before.WoundState,
                before.CurrentIntegrity,
                CharacterTestBodyPartStructureState.Severed,
                before.LastAcceptedHitFrame);
            if (!TryAdvanceEntryRevision(after, out after)
                || !TryAppendSeverTransition(definition, before, after, fact))
            {
                return false;
            }

            _workEntries[definitionIndex] = after;
            _touchedEntries[definitionIndex] = true;
            severed = true;
            return true;
        }

        private bool TryAdvanceEntryRevision(
            CharacterTestBodyPartStateEntry entry,
            out CharacterTestBodyPartStateEntry advanced)
        {
            if (entry.Revision == long.MaxValue)
            {
                _revisionExhausted = true;
                advanced = default;
                return false;
            }

            advanced = entry.WithRevision(entry.Revision + 1L);
            return true;
        }

        private bool TryAppendDamageTransition(
            CharacterTestBodyPartStateDefinition definition,
            CharacterTestBodyPartTransitionAxis axis,
            CharacterTestBodyPartTransitionReason reason,
            CharacterTestBodyPartStateEntry before,
            CharacterTestBodyPartStateEntry after,
            in CharacterTestBodyPartAcceptedDamageFact fact)
        {
            if (_transitionCount >= _transitions.Length)
            {
                TotalDamageOverflowCount = SaturatingIncrement(TotalDamageOverflowCount);
                return false;
            }

            _transitions[_transitionCount++] = new CharacterTestBodyPartStateTransition(
                fact.AuthorityFrame,
                fact.SourceEntityId,
                fact.TargetEntityId,
                definition,
                axis,
                reason,
                before,
                after,
                fact.FinalDamage,
                fact.ActionInstanceId,
                fact.PolicyEventId,
                fact.TraceId,
                fact.LocalFrame,
                CharacterTestBodyPartFocusConsumeAdapterKind.None,
                focusCorrelationId: 0L);
            return true;
        }

        private bool TryAppendFocusTransition(
            CharacterTestBodyPartStateDefinition definition,
            CharacterTestBodyPartStateEntry before,
            CharacterTestBodyPartStateEntry after,
            in CharacterTestBodyPartFocusConsumeFact fact)
        {
            if (_transitionCount >= _transitions.Length)
            {
                TotalFocusConsumeOverflowCount = SaturatingIncrement(
                    TotalFocusConsumeOverflowCount);
                return false;
            }

            _transitions[_transitionCount++] = new CharacterTestBodyPartStateTransition(
                fact.AuthorityFrame,
                fact.SourceEntityId,
                fact.TargetEntityId,
                definition,
                CharacterTestBodyPartTransitionAxis.Wound,
                CharacterTestBodyPartTransitionReason.Consumed,
                before,
                after,
                appliedDamage: 0,
                actionInstanceId: 0,
                policyEventId: 0,
                traceId: 0,
                localFrame: 0,
                focusAdapterKind: fact.AdapterKind,
                focusCorrelationId: fact.CorrelationId);
            return true;
        }

        private bool TryAppendSeverTransition(
            CharacterTestBodyPartStateDefinition definition,
            CharacterTestBodyPartStateEntry before,
            CharacterTestBodyPartStateEntry after,
            in CharacterTestBodyPartSeverFact fact)
        {
            if (_transitionCount >= _transitions.Length)
            {
                TotalSeverOverflowCount = SaturatingIncrement(TotalSeverOverflowCount);
                return false;
            }

            _transitions[_transitionCount++] = new CharacterTestBodyPartStateTransition(
                fact.AuthorityFrame,
                fact.SourceEntityId,
                fact.TargetEntityId,
                definition,
                CharacterTestBodyPartTransitionAxis.Structure,
                CharacterTestBodyPartTransitionReason.Severed,
                before,
                after,
                appliedDamage: 0,
                actionInstanceId: 0,
                policyEventId: 0,
                traceId: 0,
                localFrame: 0,
                focusAdapterKind: CharacterTestBodyPartFocusConsumeAdapterKind.None,
                focusCorrelationId: fact.CorrelationId);
            return true;
        }

        private CharacterTestBodyPartStateCommitResult FinishSuccessfulCommit(
            CharacterTestBodyPartStateCommitStatus status,
            int acceptedDamageFactCount,
            int focusConsumeFactCount,
            int severFactCount = 0)
        {
            _frameOpen = false;
            _damageFactCount = 0;
            _focusConsumeFactCount = 0;
            _severFactCount = 0;
            return new CharacterTestBodyPartStateCommitResult(
                status,
                acceptedDamageFactCount,
                focusConsumeFactCount,
                _transitionCount,
                severFactCount);
        }

        private CharacterTestBodyPartStateCommitResult FinishFailedCommit(
            CharacterTestBodyPartStateCommitStatus status)
        {
            _frameOpen = false;
            _damageFactCount = 0;
            _focusConsumeFactCount = 0;
            _severFactCount = 0;
            _transitionCount = 0;
            return new CharacterTestBodyPartStateCommitResult(status, 0, 0, 0);
        }

        private void SortDamageFacts()
        {
            for (int i = 1; i < _damageFactCount; i++)
            {
                CharacterTestBodyPartAcceptedDamageFact value = _damageFacts[i];
                int write = i;
                while (write > 0 && CompareDamageFacts(_damageFacts[write - 1], value) > 0)
                {
                    _damageFacts[write] = _damageFacts[write - 1];
                    write--;
                }
                _damageFacts[write] = value;
            }
        }

        private void SortFocusConsumeFacts()
        {
            for (int i = 1; i < _focusConsumeFactCount; i++)
            {
                CharacterTestBodyPartFocusConsumeFact value = _focusConsumeFacts[i];
                int write = i;
                while (write > 0
                    && CompareFocusFacts(_focusConsumeFacts[write - 1], value) > 0)
                {
                    _focusConsumeFacts[write] = _focusConsumeFacts[write - 1];
                    write--;
                }
                _focusConsumeFacts[write] = value;
            }
        }

        private void SortSeverFacts()
        {
            for (int i = 1; i < _severFactCount; i++)
            {
                CharacterTestBodyPartSeverFact value = _severFacts[i];
                int write = i;
                while (write > 0 && CompareSeverFacts(_severFacts[write - 1], value) > 0)
                {
                    _severFacts[write] = _severFacts[write - 1];
                    write--;
                }
                _severFacts[write] = value;
            }
        }

        private static int CompareDamageFacts(
            CharacterTestBodyPartAcceptedDamageFact left,
            CharacterTestBodyPartAcceptedDamageFact right)
        {
            int result = left.AuthorityFrame.CompareTo(right.AuthorityFrame);
            if (result != 0) return result;
            result = left.SourceEntityId.CompareTo(right.SourceEntityId);
            if (result != 0) return result;
            result = left.TargetEntityId.CompareTo(right.TargetEntityId);
            if (result != 0) return result;
            result = left.ActionInstanceId.CompareTo(right.ActionInstanceId);
            if (result != 0) return result;
            result = left.PolicyEventId.CompareTo(right.PolicyEventId);
            if (result != 0) return result;
            result = left.TraceId.CompareTo(right.TraceId);
            if (result != 0) return result;
            result = left.LocalFrame.CompareTo(right.LocalFrame);
            if (result != 0) return result;
            result = left.DefinitionIndex.CompareTo(right.DefinitionIndex);
            if (result != 0) return result;
            return left.FinalDamage.CompareTo(right.FinalDamage);
        }

        private static int CompareFocusFacts(
            CharacterTestBodyPartFocusConsumeFact left,
            CharacterTestBodyPartFocusConsumeFact right)
        {
            int result = left.AuthorityFrame.CompareTo(right.AuthorityFrame);
            if (result != 0) return result;
            result = left.SourceEntityId.CompareTo(right.SourceEntityId);
            if (result != 0) return result;
            result = left.TargetEntityId.CompareTo(right.TargetEntityId);
            if (result != 0) return result;
            result = ((int)left.AdapterKind).CompareTo((int)right.AdapterKind);
            if (result != 0) return result;
            result = left.CorrelationId.CompareTo(right.CorrelationId);
            if (result != 0) return result;
            result = left.DefinitionIndex.CompareTo(right.DefinitionIndex);
            if (result != 0) return result;
            return string.CompareOrdinal(left.FocusConsumeTag, right.FocusConsumeTag);
        }

        private static int CompareSeverFacts(
            CharacterTestBodyPartSeverFact left,
            CharacterTestBodyPartSeverFact right)
        {
            int result = left.AuthorityFrame.CompareTo(right.AuthorityFrame);
            if (result != 0) return result;
            result = left.SourceEntityId.CompareTo(right.SourceEntityId);
            if (result != 0) return result;
            result = left.TargetEntityId.CompareTo(right.TargetEntityId);
            if (result != 0) return result;
            result = left.CorrelationId.CompareTo(right.CorrelationId);
            if (result != 0) return result;
            result = left.DefinitionIndex.CompareTo(right.DefinitionIndex);
            if (result != 0) return result;
            return string.CompareOrdinal(left.SeverTag, right.SeverTag);
        }

        private static bool SameDamageKey(
            CharacterTestBodyPartAcceptedDamageFact left,
            CharacterTestBodyPartAcceptedDamageFact right)
        {
            return left.AuthorityFrame == right.AuthorityFrame
                && left.SourceEntityId == right.SourceEntityId
                && left.TargetEntityId == right.TargetEntityId
                && left.ActionInstanceId == right.ActionInstanceId
                && left.PolicyEventId == right.PolicyEventId
                && left.TraceId == right.TraceId
                && left.LocalFrame == right.LocalFrame
                && left.DefinitionIndex == right.DefinitionIndex;
        }

        private static bool SameFocusKey(
            CharacterTestBodyPartFocusConsumeFact left,
            CharacterTestBodyPartFocusConsumeFact right)
        {
            return left.AuthorityFrame == right.AuthorityFrame
                && left.SourceEntityId == right.SourceEntityId
                && left.TargetEntityId == right.TargetEntityId
                && left.AdapterKind == right.AdapterKind
                && left.CorrelationId == right.CorrelationId
                && left.DefinitionIndex == right.DefinitionIndex;
        }

        private static bool SameSeverKey(
            CharacterTestBodyPartSeverFact left,
            CharacterTestBodyPartSeverFact right)
        {
            return left.AuthorityFrame == right.AuthorityFrame
                && left.SourceEntityId == right.SourceEntityId
                && left.TargetEntityId == right.TargetEntityId
                && left.CorrelationId == right.CorrelationId
                && left.DefinitionIndex == right.DefinitionIndex;
        }

        private static bool SameEntry(
            CharacterTestBodyPartStateEntry left,
            CharacterTestBodyPartStateEntry right)
        {
            return left.DefinitionIndex == right.DefinitionIndex
                && left.WoundValue == right.WoundValue
                && left.WoundState == right.WoundState
                && left.CurrentIntegrity == right.CurrentIntegrity
                && left.StructureState == right.StructureState
                && left.LastAcceptedHitFrame == right.LastAcceptedHitFrame
                && left.Revision == right.Revision;
        }

        private static int SaturatingAddToCapacity(int value, int increment, int capacity)
        {
            long total = (long)value + increment;
            return total >= capacity ? capacity : (int)total;
        }

        private static long SaturatingIncrement(long value)
        {
            return value == long.MaxValue ? long.MaxValue : value + 1L;
        }
    }
}

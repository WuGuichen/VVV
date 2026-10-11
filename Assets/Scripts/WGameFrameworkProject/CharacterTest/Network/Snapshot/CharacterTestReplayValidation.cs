using System;
using System.Collections.Generic;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestReplayValidationState
    {
        Empty = 0,
        Ready = 1,
        InspectingRecord = 2,
        RestoringSessionAuthorityBootstrap = 3,
        RestoringGameplayBootstrap = 4,
        ComparingFrames = 5,
        Passed = 6,
        Diverged = 7,
        Failed = 8,
        Cancelled = 9
    }

    internal static class CharacterTestReplayValidationCodes
    {
        public const string None = "NONE";
        public const string Passed = "PASS";
        public const string ReplayEmpty = "REPLAY_EMPTY";
        public const string ReplayRecordCorrupt = "REPLAY_RECORD_CORRUPT";
        public const string ReplaySchemaUnsupported = "REPLAY_SCHEMA_UNSUPPORTED";
        public const string ReplayBudgetExceeded = "REPLAY_BUDGET_EXCEEDED";
        public const string PlayableResourceCatalogMismatch =
            "PLAYABLE_RESOURCE_CATALOG_MISMATCH";
        public const string SessionAuthorityBootstrapMissing = "SESSION_AUTHORITY_BOOTSTRAP_MISSING";
        public const string SessionAuthorityRestoreFailed = "SESSION_AUTHORITY_RESTORE_FAILED";
        public const string CatalogIdentityMissing = "CATALOG_IDENTITY_MISSING";
        public const string CatalogHashMismatch = "CATALOG_HASH_MISMATCH";
        public const string SessionAuthorityHashMismatch = "SESSION_AUTHORITY_HASH_MISMATCH";
        public const string GameplayBootstrapMissing = "GAMEPLAY_BOOTSTRAP_MISSING";
        public const string GameplayBootstrapRestoreFailed = "GAMEPLAY_BOOTSTRAP_RESTORE_FAILED";
        public const string CommandRejected = "COMMAND_REJECTED";
        public const string DriverException = "DRIVER_EXCEPTION";
        public const string FrameHashDiverged = "FRAME_HASH_DIVERGED";
        public const string OperationCancelled = "OPERATION_CANCELLED";
    }

    internal sealed class CharacterTestReplayPinnedCardIdentity
    {
        internal CharacterTestReplayPinnedCardIdentity(
            string stableCardId,
            int revision,
            long semanticHash,
            long contentHash,
            int cost,
            int numericId)
        {
            StableCardId = stableCardId ?? string.Empty;
            Revision = revision;
            SemanticHash = semanticHash;
            ContentHash = contentHash;
            Cost = cost;
            NumericId = numericId;
        }

        internal string StableCardId { get; }
        internal int Revision { get; }
        internal long SemanticHash { get; }
        internal long ContentHash { get; }
        internal int Cost { get; }
        internal int NumericId { get; }
    }

    internal sealed class CharacterTestReplayDeckIdentity
    {
        private readonly CharacterTestReplayPinnedCardIdentity[] _cards;
        private readonly IReadOnlyList<CharacterTestReplayPinnedCardIdentity> _readOnlyCards;

        internal CharacterTestReplayDeckIdentity(
            string characterStableId,
            int characterNumericId,
            long deckHash,
            IReadOnlyList<CharacterTestRuntimeAiPlannerPinnedBehaviorCard> cards)
        {
            CharacterStableId = characterStableId ?? string.Empty;
            CharacterNumericId = characterNumericId;
            DeckHash = deckHash;
            _cards = CopyCards(cards);
            _readOnlyCards = Array.AsReadOnly(_cards);
        }

        internal string CharacterStableId { get; }
        internal int CharacterNumericId { get; }
        internal long DeckHash { get; }
        internal IReadOnlyList<CharacterTestReplayPinnedCardIdentity> Cards => _readOnlyCards;

        private static CharacterTestReplayPinnedCardIdentity[] CopyCards(
            IReadOnlyList<CharacterTestRuntimeAiPlannerPinnedBehaviorCard> cards)
        {
            if (cards == null || cards.Count == 0)
                return Array.Empty<CharacterTestReplayPinnedCardIdentity>();
            var copy = new CharacterTestReplayPinnedCardIdentity[cards.Count];
            for (int i = 0; i < copy.Length; i++)
            {
                CharacterTestRuntimeAiPlannerPinnedBehaviorCard card = cards[i];
                copy[i] = new CharacterTestReplayPinnedCardIdentity(
                    card.StableCardId,
                    card.Revision,
                    card.SemanticHash,
                    card.ContentHash,
                    card.Cost,
                    card.NumericId);
            }
            return copy;
        }
    }

    internal sealed class CharacterTestReplaySessionAuthorityIdentity
    {
        private readonly CharacterTestReplayDeckIdentity[] _decks;
        private readonly IReadOnlyList<CharacterTestReplayDeckIdentity> _readOnlyDecks;

        internal CharacterTestReplaySessionAuthorityIdentity(
            string catalogId,
            int catalogRevision,
            long catalogHash,
            int runtimeContractVersion,
            long authorityHash,
            IReadOnlyList<CharacterTestRuntimeAiPlannerSessionDeckLock> locks)
            : this(
                catalogId,
                catalogRevision,
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.DefaultCatalogNumericId,
                0L,
                catalogHash,
                runtimeContractVersion,
                authorityHash,
                locks)
        {
        }

        internal CharacterTestReplaySessionAuthorityIdentity(
            string catalogId,
            int catalogRevision,
            int catalogNumericId,
            long catalogNumericIdentityHash,
            long catalogHash,
            int runtimeContractVersion,
            long authorityHash,
            IReadOnlyList<CharacterTestRuntimeAiPlannerSessionDeckLock> locks)
        {
            CatalogId = catalogId ?? string.Empty;
            CatalogRevision = catalogRevision;
            CatalogNumericId = catalogNumericId;
            CatalogNumericIdentityHash = catalogNumericIdentityHash;
            CatalogHash = catalogHash;
            RuntimeContractVersion = runtimeContractVersion;
            AuthorityHash = authorityHash;
            _decks = CopyDecks(locks);
            _readOnlyDecks = Array.AsReadOnly(_decks);
            IdentityHash = ComputeIdentityHash();
        }

        internal string CatalogId { get; }
        internal int CatalogRevision { get; }
        internal int CatalogNumericId { get; }
        internal long CatalogNumericIdentityHash { get; }
        internal long CatalogHash { get; }
        internal int RuntimeContractVersion { get; }
        internal long AuthorityHash { get; }
        internal IReadOnlyList<CharacterTestReplayDeckIdentity> Decks => _readOnlyDecks;
        internal int AgentCount => _decks.Length;
        internal long IdentityHash { get; }

        internal bool Matches(CharacterTestReplaySessionAuthorityIdentity other)
        {
            return other != null && IdentityHash == other.IdentityHash;
        }

        private long ComputeIdentityHash()
        {
            var hash = new RuntimeHashAccumulator(RuntimeHashSchemaIds.ReplayValidation, 2);
            hash.AddInt(ReplayValidationSessionAuthorityIdentityHashFields.CatalogRevision, CatalogRevision);
            hash.AddInt(ReplayValidationSessionAuthorityIdentityHashFields.CatalogNumericId, CatalogNumericId);
            hash.AddLong(ReplayValidationSessionAuthorityIdentityHashFields.CatalogNumericIdentityHash, CatalogNumericIdentityHash);
            hash.AddLong(ReplayValidationSessionAuthorityIdentityHashFields.CatalogHash, CatalogHash);
            hash.AddInt(ReplayValidationSessionAuthorityIdentityHashFields.RuntimeContractVersion, RuntimeContractVersion);
            hash.AddLong(ReplayValidationSessionAuthorityIdentityHashFields.AuthorityHash, AuthorityHash);
            hash.BeginArray(ReplayValidationSessionAuthorityIdentityHashFields.Decks, _decks.Length);
            for (int i = 0; i < _decks.Length; i++)
            {
                hash.BeginElement(i);
                CharacterTestReplayDeckIdentity deck = _decks[i];
                hash.AddInt(ReplayValidationDeckEntryHashFields.DeckIndex, i);
                hash.AddInt(ReplayValidationDeckEntryHashFields.CharacterNumericId, deck.CharacterNumericId);
                hash.AddLong(ReplayValidationDeckEntryHashFields.DeckHash, deck.DeckHash);
                int cardCount = deck.Cards != null ? deck.Cards.Count : 0;
                hash.BeginArray(ReplayValidationDeckEntryHashFields.Cards, cardCount);
                for (int cardIndex = 0; cardIndex < cardCount; cardIndex++)
                {
                    hash.BeginElement(cardIndex);
                    CharacterTestReplayPinnedCardIdentity card = deck.Cards[cardIndex];
                    hash.AddInt(ReplayValidationCardEntryHashFields.CardIndex, cardIndex);
                    hash.AddInt(ReplayValidationCardEntryHashFields.CardNumericId, card.NumericId);
                    hash.AddInt(ReplayValidationCardEntryHashFields.CardRevision, card.Revision);
                    hash.AddLong(ReplayValidationCardEntryHashFields.CardSemanticHash, card.SemanticHash);
                    hash.AddLong(ReplayValidationCardEntryHashFields.CardContentHash, card.ContentHash);
                    hash.AddInt(ReplayValidationCardEntryHashFields.CardCost, card.Cost);
                    hash.EndElement();
                }
                hash.EndArray();
                hash.EndElement();
            }
            hash.EndArray();
            return hash.ToHash();
        }

        private static CharacterTestReplayDeckIdentity[] CopyDecks(
            IReadOnlyList<CharacterTestRuntimeAiPlannerSessionDeckLock> locks)
        {
            if (locks == null || locks.Count == 0)
                return Array.Empty<CharacterTestReplayDeckIdentity>();
            var copy = new CharacterTestReplayDeckIdentity[locks.Count];
            for (int i = 0; i < copy.Length; i++)
            {
                CharacterTestRuntimeAiPlannerSessionDeckLock item = locks[i];
                copy[i] = new CharacterTestReplayDeckIdentity(
                    item.CharacterStableId,
                    item.CharacterNumericId,
                    item.DeckHash,
                    item.Cards);
            }
            return copy;
        }
    }

    internal readonly struct CharacterTestReplayBootstrapPhaseResult
    {
        internal CharacterTestReplayBootstrapPhaseResult(
            bool success,
            CharacterTestReplayValidationState state,
            string code,
            string message)
        {
            Success = success;
            State = state;
            Code = code ?? CharacterTestReplayValidationCodes.None;
            Message = message ?? string.Empty;
        }

        internal bool Success { get; }
        internal CharacterTestReplayValidationState State { get; }
        internal string Code { get; }
        internal string Message { get; }

        internal static CharacterTestReplayBootstrapPhaseResult Passed(
            CharacterTestReplayValidationState state)
        {
            return new CharacterTestReplayBootstrapPhaseResult(
                true,
                state,
                CharacterTestReplayValidationCodes.None,
                string.Empty);
        }

        internal static CharacterTestReplayBootstrapPhaseResult Failed(
            CharacterTestReplayValidationState state,
            string code,
            string message)
        {
            return new CharacterTestReplayBootstrapPhaseResult(false, state, code, message);
        }
    }

    internal sealed class CharacterTestReplayValidationResult
    {
        internal CharacterTestReplayValidationState State { get; set; }
        internal string Code { get; set; } = CharacterTestReplayValidationCodes.None;
        internal string Summary { get; set; } = string.Empty;
        internal int FramesCompared { get; set; }
        internal bool HasFailureFrame { get; set; }
        internal RuntimeFrame FailureFrame { get; set; } = RuntimeFrame.Zero;
        internal long ExpectedHash { get; set; }
        internal long ActualHash { get; set; }
        internal string ExpectedDiagnostics { get; set; } = string.Empty;
        internal string ActualDiagnostics { get; set; } = string.Empty;
        internal IReadOnlyList<RuntimeCommandError> CommandErrors { get; set; } =
            Array.Empty<RuntimeCommandError>();
        internal bool Success => State == CharacterTestReplayValidationState.Passed;
    }

    internal sealed class CharacterTestReplayRecordIdentity
    {
        private const int MaximumFrameCount = 4096;
        private const int MaximumCommandCount = 65536;
        private const int MaximumBootstrapPayloadLength = 8 * 1024 * 1024;

        private CharacterTestReplayRecordIdentity(
            int startFrame,
            int endFrame,
            int frameCount,
            int recordedFrameCommandCount,
            int authoritativePacketCommandCount,
            long authorityRecordHash,
            string code,
            string message)
        {
            StartFrame = startFrame;
            EndFrame = endFrame;
            FrameCount = frameCount;
            RecordedFrameCommandCount = recordedFrameCommandCount;
            AuthoritativePacketCommandCount = authoritativePacketCommandCount;
            AuthorityRecordHash = authorityRecordHash;
            Code = code ?? CharacterTestReplayValidationCodes.None;
            Message = message ?? string.Empty;
        }

        internal int StartFrame { get; }
        internal int EndFrame { get; }
        internal int FrameCount { get; }
        internal int RecordedFrameCommandCount { get; }
        internal int AuthoritativePacketCommandCount { get; }
        internal long AuthorityRecordHash { get; }
        internal string Code { get; }
        internal string Message { get; }
        internal bool IsValid => string.Equals(
            Code,
            CharacterTestReplayValidationCodes.None,
            StringComparison.Ordinal);

        /// <remarks>LowFreqAlloc: runs once when a replay capture is frozen or validated.</remarks>
        internal static CharacterTestReplayRecordIdentity Inspect(CharacterTestReplayCapture capture)
        {
            if (capture == null || capture.RuntimeReplaySnapshot == null)
                return Failed(CharacterTestReplayValidationCodes.ReplayEmpty, "Replay capture is unavailable.");
            RuntimeReplaySnapshot snapshot = capture.RuntimeReplaySnapshot;
            if (snapshot.Count == 0)
                return Failed(CharacterTestReplayValidationCodes.ReplayEmpty, "Replay capture has no frame records.");
            if (capture.SessionAuthorityBootstrapSnapshot == null)
            {
                return Failed(
                    CharacterTestReplayValidationCodes.SessionAuthorityBootstrapMissing,
                    "Replay validation requires a session authority bootstrap, including an empty roster.");
            }
            if (capture.SessionAuthorityParticipantFactory == null)
            {
                return Failed(
                    CharacterTestReplayValidationCodes.SessionAuthorityRestoreFailed,
                    "Replay validation requires a session authority participant factory.");
            }
            if (capture.SessionAuthorityIdentity == null)
            {
                return Failed(
                    CharacterTestReplayValidationCodes.CatalogIdentityMissing,
                    "Replay validation requires the frozen catalog and pinned-deck identity.");
            }
            if (capture.SessionAuthorityIdentity.AuthorityHash != capture.SessionAuthorityHash)
            {
                return Failed(
                    CharacterTestReplayValidationCodes.SessionAuthorityHashMismatch,
                    "Replay session authority identity does not match the frozen authority hash.");
            }
            if (snapshot.Header.StartFrame.Value < 0L
                || snapshot.Header.StartFrame.Value > int.MaxValue)
            {
                return Failed(
                    CharacterTestReplayValidationCodes.ReplayRecordCorrupt,
                    "Replay start frame is outside the Runtime AI Lab supported range.");
            }
            string frameworkVersion = snapshot.Header.FrameworkVersion ?? string.Empty;
            bool frameworkVersionSupported = string.Equals(
                    frameworkVersion,
                    CharacterTestReplayRecorder.FrameworkVersion,
                    StringComparison.Ordinal)
                || frameworkVersion.StartsWith(
                    CharacterTestReplayRecorder.FrameworkVersion + ";",
                    StringComparison.Ordinal);
            if (snapshot.Header.SchemaVersion
                == CharacterTestReplayRecorder.LegacyRootDistanceSchemaVersion)
            {
                return Failed(
                    CharacterTestReplayValidationCodes.ReplaySchemaUnsupported,
                    "Replay schema v3 is unsupported because target.distance used legacy root-distance semantics.");
            }
            if (snapshot.Header.SchemaVersion == CharacterTestReplayRecorder.LegacyCharacterActionSchemaVersion)
            {
                return Failed(
                    CharacterTestReplayValidationCodes.ReplaySchemaUnsupported,
                    "Replay schema v2 is unsupported because it stores legacy string CharacterAction IDs.");
            }
            if (snapshot.Header.SchemaVersion != CharacterTestReplayRecorder.SchemaVersion
                || !frameworkVersionSupported)
            {
                return Failed(
                    CharacterTestReplayValidationCodes.ReplaySchemaUnsupported,
                    "Replay schema or framework version is unsupported.");
            }
            string currentResourceCatalogHash;
            try
            {
                currentResourceCatalogHash =
                    CharacterTestNetworkSessionFactory.ComputeCurrentPlayableCharacterResourceCatalogHash();
            }
            catch (Exception exception)
            {
                return Failed(
                    CharacterTestReplayValidationCodes.PlayableResourceCatalogMismatch,
                    "Current playable package resource identity is unavailable: " + exception.Message);
            }
            if (string.IsNullOrEmpty(snapshot.Header.ResourceCatalogHash)
                || !string.Equals(
                    snapshot.Header.ResourceCatalogHash,
                    currentResourceCatalogHash,
                    StringComparison.Ordinal))
            {
                return Failed(
                    CharacterTestReplayValidationCodes.PlayableResourceCatalogMismatch,
                    "Replay playable package resource identity does not match the current catalog.");
            }
            if (snapshot.Count > MaximumFrameCount
                || capture.AuthoritativeCommandCount > MaximumCommandCount
                || PayloadLength(capture.BootstrapSnapshot) > MaximumBootstrapPayloadLength
                || PayloadLength(capture.SessionAuthorityBootstrapSnapshot) > MaximumBootstrapPayloadLength)
            {
                return Failed(
                    CharacterTestReplayValidationCodes.ReplayBudgetExceeded,
                    "Replay capture exceeds the Runtime AI Lab validation budget.");
            }

            int recordedCommandCount = 0;
            long previousFrame = -1L;
            var hash = new RuntimeHashAccumulator(RuntimeHashSchemaIds.ReplayValidation, 1);
            RuntimeReplayHeader header = snapshot.Header;
            hash.AddInt(ReplayValidationRecordHashFields.Schema, header.SchemaVersion);
            hash.AddDigest(
                ReplayValidationRecordHashFields.Framework,
                capture.FrameworkVersionDigest);
            hash.AddDigest(
                ReplayValidationRecordHashFields.ConfigHash,
                capture.ConfigDigest);
            capture.ResourceCatalogDigest.Contribute(hash, ReplayValidationRecordHashFields.ResourceCatalogHash);
            hash.AddLong(ReplayValidationRecordHashFields.StartFrame, header.StartFrame.Value);
            hash.AddInt(
                ReplayValidationRecordHashFields.GameplayBootstrapType,
                capture.BootstrapSnapshot?.NumericTypeId ?? 0);
            hash.AddInt(ReplayValidationRecordHashFields.GameplayBootstrapSchema, capture.BootstrapSnapshot?.SchemaVersion ?? -1);
            hash.AddDigest(
                ReplayValidationRecordHashFields.GameplayBootstrapPayload,
                capture.GameplayBootstrapDigest);
            RuntimeCustomState authorityBootstrap = capture.SessionAuthorityBootstrapSnapshot;
            hash.AddInt(
                ReplayValidationRecordHashFields.SessionAuthorityType,
                authorityBootstrap?.NumericTypeId ?? 0);
            hash.AddInt(ReplayValidationRecordHashFields.SessionAuthoritySchema, authorityBootstrap?.SchemaVersion ?? -1);
            hash.AddDigest(
                ReplayValidationRecordHashFields.SessionAuthorityPayload,
                capture.AuthorityBootstrapDigest);
            hash.AddLong(ReplayValidationRecordHashFields.SessionAuthorityHash, capture.SessionAuthorityHash);
            hash.AddLong(ReplayValidationRecordHashFields.SessionAuthorityIdentity, capture.SessionAuthorityIdentity?.IdentityHash ?? 0L);

            hash.BeginArray(ReplayValidationRecordHashFields.Records, snapshot.Count);
            for (int i = 0; i < snapshot.Records.Count; i++)
            {
                RuntimeReplayFrameRecord record = snapshot.Records[i];
                if (record.Frame.Value > int.MaxValue
                    || record.Frame.Value <= previousFrame
                    || record.Frame < header.StartFrame)
                {
                    return Failed(
                        CharacterTestReplayValidationCodes.ReplayRecordCorrupt,
                        "Replay frames must be strictly increasing and not precede the header start frame.");
                }
                previousFrame = record.Frame.Value;
                hash.BeginElement(i);
                hash.AddInt(ReplayValidationRecordFrameEntryHashFields.FrameIndex, i);
                hash.AddLong(ReplayValidationRecordFrameEntryHashFields.Frame, record.Frame.Value);
                hash.AddLong(ReplayValidationRecordFrameEntryHashFields.ResultHash, record.ResultHash);

                int recordCommandCount = record.Commands != null ? record.Commands.Count : 0;
                recordedCommandCount += recordCommandCount;
                if (recordedCommandCount > MaximumCommandCount)
                    return Failed(CharacterTestReplayValidationCodes.ReplayBudgetExceeded, "Replay command budget exceeded.");

                hash.BeginArray(ReplayValidationRecordFrameEntryHashFields.Commands, recordCommandCount);
                for (int commandIndex = 0; commandIndex < recordCommandCount; commandIndex++)
                {
                    RuntimeCommand command = record.Commands[commandIndex];
                    if (command.Frame != record.Frame)
                    {
                        return Failed(
                            CharacterTestReplayValidationCodes.ReplayRecordCorrupt,
                            "Every drained replay command must belong to its record frame.");
                    }
                    hash.BeginElement(commandIndex);
                    AddCommandHash(ref hash, commandIndex, command);
                    hash.EndElement();
                }
                hash.EndArray();
                hash.EndElement();
            }
            hash.EndArray();

            RuntimeCommand[] authoritative = CopyAndSortCommands(capture.AuthoritativeCommands);
            hash.BeginArray(ReplayValidationRecordHashFields.AuthoritativeCommands, authoritative.Length);
            for (int i = 0; i < authoritative.Length; i++)
            {
                RuntimeCommand command = authoritative[i];
                if (command.Frame < header.StartFrame || command.Frame.Value > previousFrame)
                {
                    return Failed(
                        CharacterTestReplayValidationCodes.ReplayRecordCorrupt,
                        "Authoritative packet command is outside the replay frame range.");
                }
                hash.BeginElement(i);
                AddCommandHash(ref hash, i, command);
                hash.EndElement();
            }
            hash.EndArray();

            return new CharacterTestReplayRecordIdentity(
                (int)header.StartFrame.Value,
                (int)previousFrame,
                snapshot.Count,
                recordedCommandCount,
                authoritative.Length,
                hash.ToHash(),
                CharacterTestReplayValidationCodes.None,
                string.Empty);
        }

        private static CharacterTestReplayRecordIdentity Failed(string code, string message)
        {
            return new CharacterTestReplayRecordIdentity(0, 0, 0, 0, 0, 0L, code, message);
        }

        private static int PayloadLength(RuntimeCustomState state)
        {
            return state?.PayloadJson?.Length ?? 0;
        }

        private static RuntimeCommand[] CopyAndSortCommands(IReadOnlyList<RuntimeCommand> source)
        {
            if (source == null || source.Count == 0)
                return Array.Empty<RuntimeCommand>();
            var copy = new RuntimeCommand[source.Count];
            for (int i = 0; i < copy.Length; i++)
                copy[i] = source[i];
            Array.Sort(copy, CompareCommands);
            return copy;
        }

        private static int CompareCommands(RuntimeCommand left, RuntimeCommand right)
        {
            int result = left.Frame.CompareTo(right.Frame);
            if (result != 0) return result;
            result = left.SourceId.CompareTo(right.SourceId);
            if (result != 0) return result;
            result = left.CommandId.CompareTo(right.CommandId);
            if (result != 0) return result;
            result = left.TargetId.CompareTo(right.TargetId);
            if (result != 0) return result;
            return left.Sequence.CompareTo(right.Sequence);
        }

        private static void AddCommandHash(
            ref RuntimeHashAccumulator hash,
            int index,
            RuntimeCommand command)
        {
            hash.AddInt(ReplayValidationCommandEntryHashFields.Index, index);
            hash.AddLong(ReplayValidationCommandEntryHashFields.Frame, command.Frame.Value);
            hash.AddInt(ReplayValidationCommandEntryHashFields.Source, command.SourceId);
            hash.AddInt(ReplayValidationCommandEntryHashFields.Command, command.CommandId);
            hash.AddInt(ReplayValidationCommandEntryHashFields.Target, command.TargetId);
            hash.AddInt(ReplayValidationCommandEntryHashFields.Payload0, command.Payload0);
            hash.AddInt(ReplayValidationCommandEntryHashFields.Payload1, command.Payload1);
            hash.AddInt(ReplayValidationCommandEntryHashFields.Payload2, command.Payload2);
            hash.AddLong(ReplayValidationCommandEntryHashFields.Sequence, command.Sequence);
            command.TraceId.Contribute(hash, ReplayValidationCommandEntryHashFields.TraceId);
        }
    }

    /// <summary>CharacterTest-only replay comparison kernel shared by synchronous and incremental validation.</summary>
    /// <remarks>AllocByDesign: explicit replay validation creates an isolated runtime slice and result snapshots.</remarks>
    internal sealed class CharacterTestReplayValidationComparisonKernel : IDisposable
    {
        private readonly CharacterTestReplayCapture _capture;
        private readonly CharacterTestReplayFrameDriver _driver;
        private CharacterTestReplayRecordIdentity _identity;
        private int _nextRecordIndex;
        private bool _bootstrapped;
        private bool _disposed;
        private CharacterTestReplayValidationResult _terminal;

        internal CharacterTestReplayValidationComparisonKernel(CharacterTestReplayCapture capture)
        {
            _capture = capture;
            _driver = capture != null ? new CharacterTestReplayFrameDriver(capture) : null;
        }

        internal CharacterTestReplayRecordIdentity Identity => _identity;
        internal int ComparedFrames => _nextRecordIndex;
        internal int TotalFrames => _capture?.FrameCount ?? 0;
        internal bool IsTerminal => _terminal != null;
        internal CharacterTestReplayValidationResult TerminalResult => _terminal;

        internal CharacterTestReplayValidationResult Inspect()
        {
            ThrowIfDisposed();
            _identity = CharacterTestReplayRecordIdentity.Inspect(_capture);
            if (_identity.IsValid)
                return State(CharacterTestReplayValidationState.Ready);
            _terminal = Failed(
                _identity.Code == CharacterTestReplayValidationCodes.ReplayEmpty
                    ? CharacterTestReplayValidationState.Empty
                    : CharacterTestReplayValidationState.Failed,
                _identity.Code,
                _identity.Message);
            return _terminal;
        }

        internal CharacterTestReplayBootstrapPhaseResult RestoreSessionAuthorityBootstrap()
        {
            ThrowIfDisposed();
            EnsureInspected();
            return _driver.BeginResetAndRestoreSessionAuthority(
                _capture.RuntimeReplaySnapshot.Header);
        }

        internal CharacterTestReplayBootstrapPhaseResult RestoreGameplayBootstrap()
        {
            ThrowIfDisposed();
            EnsureInspected();
            CharacterTestReplayBootstrapPhaseResult result = _driver.RestoreGameplayBootstrap(
                _capture.RuntimeReplaySnapshot.Header);
            _bootstrapped = result.Success;
            return result;
        }

        internal CharacterTestReplayValidationResult CompareNextFrame()
        {
            ThrowIfDisposed();
            EnsureInspected();
            if (!_bootstrapped)
                throw new InvalidOperationException("Replay bootstrap has not completed.");
            if (_terminal != null)
                return _terminal;
            if (_nextRecordIndex >= _capture.RuntimeReplaySnapshot.Records.Count)
                return Complete();

            RuntimeReplayFrameRecord record = _capture.RuntimeReplaySnapshot.Records[_nextRecordIndex];
            RuntimeReplayPlaybackFrameResult frameResult;
            try
            {
                frameResult = _driver.RunFrame(record);
            }
            catch (Exception exception)
            {
                _terminal = FailureAt(
                    CharacterTestReplayValidationState.Failed,
                    CharacterTestReplayValidationCodes.DriverException,
                    exception.Message,
                    record,
                    0L,
                    string.Empty,
                    Array.Empty<RuntimeCommandError>());
                return _terminal;
            }
            if (frameResult == null)
            {
                _terminal = FailureAt(
                    CharacterTestReplayValidationState.Failed,
                    CharacterTestReplayValidationCodes.DriverException,
                    "Replay driver returned a null frame result.",
                    record,
                    0L,
                    string.Empty,
                    Array.Empty<RuntimeCommandError>());
                return _terminal;
            }

            _nextRecordIndex++;
            if (frameResult.CommandErrors.Count > 0)
            {
                _terminal = FailureAt(
                    CharacterTestReplayValidationState.Failed,
                    CharacterTestReplayValidationCodes.CommandRejected,
                    "Replay command was rejected.",
                    record,
                    frameResult.ActualResultHash,
                    frameResult.DiagnosticsSummary,
                    frameResult.CommandErrors);
                return _terminal;
            }
            if (record.ResultHash != frameResult.ActualResultHash)
            {
                _terminal = FailureAt(
                    CharacterTestReplayValidationState.Diverged,
                    CharacterTestReplayValidationCodes.FrameHashDiverged,
                    "Replay aggregate authority hash diverged.",
                    record,
                    frameResult.ActualResultHash,
                    frameResult.DiagnosticsSummary,
                    Array.Empty<RuntimeCommandError>());
                return _terminal;
            }
            return _nextRecordIndex >= _capture.FrameCount
                ? Complete()
                : State(CharacterTestReplayValidationState.ComparingFrames);
        }

        internal CharacterTestReplayValidationResult Cancel()
        {
            ThrowIfDisposed();
            if (_terminal == null)
            {
                _terminal = Failed(
                    CharacterTestReplayValidationState.Cancelled,
                    CharacterTestReplayValidationCodes.OperationCancelled,
                    "Replay validation was cancelled.");
                _terminal.FramesCompared = _nextRecordIndex;
            }
            return _terminal;
        }

        internal CharacterTestReplayValidationResult RunToCompletion()
        {
            CharacterTestReplayValidationResult inspected = Inspect();
            if (IsTerminal)
                return inspected;
            CharacterTestReplayBootstrapPhaseResult authority = RestoreSessionAuthorityBootstrap();
            if (!authority.Success)
                return BootstrapFailed(authority);
            CharacterTestReplayBootstrapPhaseResult gameplay = RestoreGameplayBootstrap();
            if (!gameplay.Success)
                return BootstrapFailed(gameplay);
            while (!IsTerminal)
                CompareNextFrame();
            return _terminal;
        }

        internal CharacterTestReplayValidationResult BootstrapFailed(
            CharacterTestReplayBootstrapPhaseResult result)
        {
            _terminal = Failed(CharacterTestReplayValidationState.Failed, result.Code, result.Message);
            return _terminal;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _driver?.Dispose();
            _disposed = true;
        }

        private CharacterTestReplayValidationResult Complete()
        {
            RuntimeReplayFrameRecord finalRecord = _capture != null && _capture.FrameCount > 0
                ? _capture.RuntimeReplaySnapshot.Records[_capture.FrameCount - 1]
                : null;
            _terminal = new CharacterTestReplayValidationResult
            {
                State = CharacterTestReplayValidationState.Passed,
                Code = CharacterTestReplayValidationCodes.Passed,
                Summary = "CharacterTest replay deterministic validation passed.",
                FramesCompared = _nextRecordIndex,
                ExpectedHash = finalRecord?.ResultHash ?? 0L,
                ActualHash = finalRecord?.ResultHash ?? 0L
            };
            return _terminal;
        }

        private CharacterTestReplayValidationResult FailureAt(
            CharacterTestReplayValidationState state,
            string code,
            string summary,
            RuntimeReplayFrameRecord record,
            long actualHash,
            string actualDiagnostics,
            IReadOnlyList<RuntimeCommandError> commandErrors)
        {
            return new CharacterTestReplayValidationResult
            {
                State = state,
                Code = code,
                Summary = summary,
                FramesCompared = _nextRecordIndex,
                HasFailureFrame = true,
                FailureFrame = record.Frame,
                ExpectedHash = record.ResultHash,
                ActualHash = actualHash,
                ExpectedDiagnostics = record.DiagnosticsSummary,
                ActualDiagnostics = actualDiagnostics ?? string.Empty,
                CommandErrors = commandErrors ?? Array.Empty<RuntimeCommandError>()
            };
        }

        private static CharacterTestReplayValidationResult Failed(
            CharacterTestReplayValidationState state,
            string code,
            string summary)
        {
            return new CharacterTestReplayValidationResult
            {
                State = state,
                Code = code,
                Summary = summary ?? string.Empty
            };
        }

        private static CharacterTestReplayValidationResult State(
            CharacterTestReplayValidationState state)
        {
            return new CharacterTestReplayValidationResult { State = state };
        }

        private void EnsureInspected()
        {
            if (_identity == null || !_identity.IsValid)
                throw new InvalidOperationException("Replay capture has not passed inspection.");
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(CharacterTestReplayValidationComparisonKernel));
        }
    }

    internal static class ReplayValidationSessionAuthorityIdentityHashFields
    {
        public const int CatalogId = 1;
        public const int CatalogRevision = 2;
        public const int CatalogHash = 3;
        public const int RuntimeContractVersion = 4;
        public const int AuthorityHash = 5;
        public const int Decks = 6;
        public const int CatalogNumericId = 7;
        public const int CatalogNumericIdentityHash = 8;
    }

    internal static class ReplayValidationDeckEntryHashFields
    {
        public const int DeckIndex = 1;
        public const int CharacterStableId = 2;
        public const int DeckHash = 3;
        public const int Cards = 4;
        public const int CharacterNumericId = 5;
    }

    internal static class ReplayValidationCardEntryHashFields
    {
        public const int CardIndex = 1;
        public const int CardStableId = 2;
        public const int CardRevision = 3;
        public const int CardSemanticHash = 4;
        public const int CardContentHash = 5;
        public const int CardCost = 6;
        public const int CardNumericId = 7;
    }

    internal static class ReplayValidationRecordHashFields
    {
        public const int Schema = 1;
        public const int Framework = 2;
        public const int ConfigHash = 3;
        public const int ResourceCatalogHash = 4;
        public const int StartFrame = 5;
        public const int GameplayBootstrapType = 6;
        public const int GameplayBootstrapSchema = 7;
        public const int GameplayBootstrapPayload = 8;
        public const int SessionAuthorityType = 9;
        public const int SessionAuthoritySchema = 10;
        public const int SessionAuthorityPayload = 11;
        public const int SessionAuthorityHash = 12;
        public const int SessionAuthorityIdentity = 13;
        public const int Records = 14;
        public const int AuthoritativeCommands = 15;
    }

    internal static class ReplayValidationRecordFrameEntryHashFields
    {
        public const int FrameIndex = 1;
        public const int Frame = 2;
        public const int ResultHash = 3;
        public const int Commands = 4;
    }

    internal static class ReplayValidationCommandEntryHashFields
    {
        public const int Index = 1;
        public const int Frame = 2;
        public const int Source = 3;
        public const int Command = 4;
        public const int Target = 5;
        public const int Payload0 = 6;
        public const int Payload1 = 7;
        public const int Payload2 = 8;
        public const int Sequence = 9;
        public const int TraceId = 10;
    }
}

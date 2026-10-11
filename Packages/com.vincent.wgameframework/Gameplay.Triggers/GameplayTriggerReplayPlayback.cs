using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.Gameplay.Triggers
{
    public enum GameplayTriggerReplayBootstrapMode
    {
        None = 0,
        Fresh = 1,
        CheckpointSuffix = 2
    }

    public enum GameplayTriggerReplayFailureCode
    {
        None = 0,
        InvalidHeader = 1,
        InvalidCheckpoint = 2,
        SessionFactoryFailed = 3,
        CompositionIdentityMismatch = 4,
        LifecycleFailed = 5,
        CheckpointRestoreFailed = 6,
        InvalidDriverState = 7,
        FrameSequenceMismatch = 8,
        FrameExhausted = 9,
        CommandCapacityExceeded = 10,
        InvalidCommandOrder = 11,
        UnauthorizedCommand = 12,
        InvalidCommandPayload = 13,
        CommandAdmissionFailed = 14,
        HostTickFailed = 15,
        ResultHashMismatch = 16
    }

    /// <summary>Immutable replay bootstrap configuration owned by the composition root.</summary>
    public readonly struct GameplayTriggerReplayBootstrapOptions
    {
        /// <remarks>NoAlloc. Strings remain caller-owned immutable references.</remarks>
        public GameplayTriggerReplayBootstrapOptions(
            string expectedFrameworkVersion,
            string expectedResourceCatalogHash,
            long expectedGameplayTriggerCatalogHash,
            int maxCommandsPerFrame,
            int maxSignalCommandItems,
            int maxOccupancyCommandItems,
            int maxScalarCommandItems)
        {
            if (string.IsNullOrWhiteSpace(expectedFrameworkVersion))
                throw new ArgumentException("Expected framework version cannot be null, empty, or whitespace.", nameof(expectedFrameworkVersion));
            if (maxCommandsPerFrame <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxCommandsPerFrame));
            if (maxSignalCommandItems <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxSignalCommandItems));
            if (maxOccupancyCommandItems <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxOccupancyCommandItems));
            if (maxScalarCommandItems <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxScalarCommandItems));

            ExpectedFrameworkVersion = expectedFrameworkVersion;
            ExpectedResourceCatalogHash = expectedResourceCatalogHash ?? string.Empty;
            ExpectedGameplayTriggerCatalogHash = expectedGameplayTriggerCatalogHash;
            MaxCommandsPerFrame = maxCommandsPerFrame;
            MaxSignalCommandItems = maxSignalCommandItems;
            MaxOccupancyCommandItems = maxOccupancyCommandItems;
            MaxScalarCommandItems = maxScalarCommandItems;
        }

        public string ExpectedFrameworkVersion { get; }
        public string ExpectedResourceCatalogHash { get; }
        public long ExpectedGameplayTriggerCatalogHash { get; }
        public int MaxCommandsPerFrame { get; }
        /// <summary>Gets the maximum item count reconstructed by one Signal batch.</summary>
        public int MaxSignalCommandItems { get; }
        /// <summary>Gets the maximum item count reconstructed by one occupancy batch.</summary>
        public int MaxOccupancyCommandItems { get; }
        /// <summary>Gets the maximum item count reconstructed by one scalar batch.</summary>
        public int MaxScalarCommandItems { get; }
    }

    public static class GameplayTriggerReplayHeaderContract
    {
        public const int CurrentSchemaVersion = 1;
        public const string ConfigHashPrefix = "mxframework.gameplay.triggers.catalog/v2/";

        /// <remarks>AllocByDesign. Creates the canonical low-frequency replay/config identity string.</remarks>
        public static string FormatConfigHash(long expectedGameplayTriggerCatalogHash)
        {
            return ConfigHashPrefix + unchecked((ulong)expectedGameplayTriggerCatalogHash)
                .ToString("x16", CultureInfo.InvariantCulture);
        }

        /// <remarks>AllocByDesign. Creates the canonical low-frequency replay header.</remarks>
        public static RuntimeReplayHeader Create(
            string frameworkVersion,
            string resourceCatalogHash,
            long expectedGameplayTriggerCatalogHash,
            RuntimeFrame startFrame)
        {
            if (string.IsNullOrWhiteSpace(frameworkVersion))
                throw new ArgumentException("Framework version cannot be null, empty, or whitespace.", nameof(frameworkVersion));

            return new RuntimeReplayHeader(
                CurrentSchemaVersion,
                frameworkVersion,
                FormatConfigHash(expectedGameplayTriggerCatalogHash),
                resourceCatalogHash ?? string.Empty,
                startFrame);
        }
    }

    public readonly struct GameplayTriggerReplaySessionCreateContext
    {
        internal GameplayTriggerReplaySessionCreateContext(
            RuntimeReplayHeader header,
            GameplayTriggerReplayBootstrapMode bootstrapMode,
            RuntimeFrame startFrame,
            long expectedGameplayTriggerCatalogHash,
            string expectedConfigHash,
            int maxCommandsPerFrame,
            int maxSignalCommandItems,
            int maxOccupancyCommandItems,
            int maxScalarCommandItems)
        {
            Header = header;
            BootstrapMode = bootstrapMode;
            StartFrame = startFrame;
            ExpectedGameplayTriggerCatalogHash = expectedGameplayTriggerCatalogHash;
            ExpectedConfigHash = expectedConfigHash ?? string.Empty;
            MaxCommandsPerFrame = maxCommandsPerFrame;
            MaxSignalCommandItems = maxSignalCommandItems;
            MaxOccupancyCommandItems = maxOccupancyCommandItems;
            MaxScalarCommandItems = maxScalarCommandItems;
        }

        public RuntimeReplayHeader Header { get; }
        public GameplayTriggerReplayBootstrapMode BootstrapMode { get; }
        public RuntimeFrame StartFrame { get; }
        public long ExpectedGameplayTriggerCatalogHash { get; }
        public string ExpectedConfigHash { get; }
        public int MaxCommandsPerFrame { get; }
        public int MaxSignalCommandItems { get; }
        public int MaxOccupancyCommandItems { get; }
        public int MaxScalarCommandItems { get; }
    }

    public interface IGameplayTriggerReplaySessionFactory
    {
        /// <remarks>AllocByDesign composition-root hook. Never call from Tick.</remarks>
        void Create(
            in GameplayTriggerReplaySessionCreateContext context,
            GameplayTriggerReplaySessionBuilder builder);
    }

    /// <summary>Failure-only structured context for Gameplay Trigger replay bootstrap/playback.</summary>
    public sealed class GameplayTriggerReplayException : Exception
    {
        internal GameplayTriggerReplayException(
            GameplayTriggerReplayFailureCode code,
            string message,
            bool hasFrame = false,
            RuntimeFrame frame = default,
            int commandIndex = -1,
            RuntimeSaveStateError checkpointError = null,
            Exception innerException = null)
            : base(message ?? string.Empty, innerException)
        {
            Code = code;
            HasFrame = hasFrame;
            Frame = frame;
            CommandIndex = commandIndex;
            CheckpointError = checkpointError;
        }

        public GameplayTriggerReplayFailureCode Code { get; }
        public bool HasFrame { get; }
        public RuntimeFrame Frame { get; }
        public int CommandIndex { get; }
        public RuntimeSaveStateError CheckpointError { get; }
    }

    public sealed class GameplayTriggerReplaySessionBuilder
    {
        private RuntimeHost _host;
        private GameplayRuntimeModule _gameplayModule;
        private GameplayTriggerRuntime _triggerRuntime;
        private GameplayTriggerDeferredSystem _deferredSystem;
        private GameplayTriggerCommandSystem _commandSystem;
        private GameplayTriggerEvaluationSystem _evaluationSystem;
        private GameplayTriggerEventRouterSystem _eventRouter;
        private GameplayTriggerReliableOutboxModule _reliableOutbox;
        private RuntimeCommandSourceRegistry _sourceRegistry;
        private RuntimeSaveStateCoordinator _restoreCoordinator;
        private bool _hasComposition;
        private bool _completed;

        internal GameplayTriggerReplaySessionBuilder()
        {
        }

        /// <summary>Transfers one complete replay composition to the builder.</summary>
        /// <remarks>NoAlloc. May be called exactly once; the builder exposes no authority getters.</remarks>
        public void SetComposition(
            RuntimeHost host,
            GameplayRuntimeModule gameplayModule,
            GameplayTriggerRuntime triggerRuntime,
            GameplayTriggerDeferredSystem deferredSystem,
            GameplayTriggerCommandSystem commandSystem,
            GameplayTriggerEvaluationSystem evaluationSystem,
            GameplayTriggerEventRouterSystem eventRouter,
            GameplayTriggerReliableOutboxModule reliableOutbox,
            RuntimeCommandSourceRegistry sourceRegistry,
            RuntimeSaveStateCoordinator restoreCoordinator)
        {
            if (_hasComposition || _completed)
                throw new InvalidOperationException("Gameplay Trigger replay composition has already been transferred.");
            if (host == null) throw new ArgumentNullException(nameof(host));
            if (gameplayModule == null) throw new ArgumentNullException(nameof(gameplayModule));
            if (triggerRuntime == null) throw new ArgumentNullException(nameof(triggerRuntime));
            if (deferredSystem == null) throw new ArgumentNullException(nameof(deferredSystem));
            if (commandSystem == null) throw new ArgumentNullException(nameof(commandSystem));
            if (evaluationSystem == null) throw new ArgumentNullException(nameof(evaluationSystem));
            if (eventRouter == null) throw new ArgumentNullException(nameof(eventRouter));
            if (sourceRegistry == null) throw new ArgumentNullException(nameof(sourceRegistry));
            if (restoreCoordinator == null) throw new ArgumentNullException(nameof(restoreCoordinator));

            _host = host;
            _gameplayModule = gameplayModule;
            _triggerRuntime = triggerRuntime;
            _deferredSystem = deferredSystem;
            _commandSystem = commandSystem;
            _evaluationSystem = evaluationSystem;
            _eventRouter = eventRouter;
            _reliableOutbox = reliableOutbox;
            _sourceRegistry = sourceRegistry;
            _restoreCoordinator = restoreCoordinator;
            _hasComposition = true;
        }

        internal GameplayTriggerReplaySession Complete(
            in GameplayTriggerReplaySessionCreateContext context,
            RuntimeSaveState checkpoint)
        {
            if (_completed)
                throw new InvalidOperationException("Gameplay Trigger replay builder has already completed.");
            if (!_hasComposition)
                throw new GameplayTriggerReplayException(
                    GameplayTriggerReplayFailureCode.CompositionIdentityMismatch,
                    "Gameplay Trigger replay factory returned without transferring a composition.");

            var session = new GameplayTriggerReplaySession(
                _host,
                _gameplayModule,
                _triggerRuntime,
                _deferredSystem,
                _commandSystem,
                _evaluationSystem,
                _eventRouter,
                _reliableOutbox,
                _sourceRegistry,
                _restoreCoordinator);

            _completed = true;
            ClearReferences();
            try
            {
                session.Bootstrap(in context, checkpoint);
                return session;
            }
            catch
            {
                try
                {
                    session.Dispose();
                }
                catch
                {
                    // Preserve the typed bootstrap failure after best-effort ownership cleanup.
                }
                throw;
            }
        }

        internal void DisposeTransferredOwnership()
        {
            if (!_hasComposition || _completed)
                return;

            _completed = true;
            GameplayTriggerReplaySession.DisposeComposition(
                _host,
                _gameplayModule,
                _reliableOutbox);
            ClearReferences();
        }

        private void ClearReferences()
        {
            _host = null;
            _gameplayModule = null;
            _triggerRuntime = null;
            _deferredSystem = null;
            _commandSystem = null;
            _evaluationSystem = null;
            _eventRouter = null;
            _reliableOutbox = null;
            _sourceRegistry = null;
            _restoreCoordinator = null;
            _hasComposition = false;
        }
    }

    internal sealed class GameplayTriggerReplaySession : IDisposable
    {
        private readonly RuntimeHost _host;
        private readonly GameplayRuntimeModule _gameplayModule;
        private readonly GameplayTriggerRuntime _triggerRuntime;
        private readonly GameplayTriggerDeferredSystem _deferredSystem;
        private readonly GameplayTriggerCommandSystem _commandSystem;
        private readonly GameplayTriggerEvaluationSystem _evaluationSystem;
        private readonly GameplayTriggerEventRouterSystem _eventRouter;
        private readonly GameplayTriggerReliableOutboxModule _reliableOutbox;
        private readonly RuntimeCommandSourceRegistry _sourceRegistry;
        private readonly RuntimeSaveStateCoordinator _restoreCoordinator;
        private bool _gameplayRegistered;
        private bool _reliableRegistered;
        private bool _disposed;

        internal GameplayTriggerReplaySession(
            RuntimeHost host,
            GameplayRuntimeModule gameplayModule,
            GameplayTriggerRuntime triggerRuntime,
            GameplayTriggerDeferredSystem deferredSystem,
            GameplayTriggerCommandSystem commandSystem,
            GameplayTriggerEvaluationSystem evaluationSystem,
            GameplayTriggerEventRouterSystem eventRouter,
            GameplayTriggerReliableOutboxModule reliableOutbox,
            RuntimeCommandSourceRegistry sourceRegistry,
            RuntimeSaveStateCoordinator restoreCoordinator)
        {
            _host = host;
            _gameplayModule = gameplayModule;
            _triggerRuntime = triggerRuntime;
            _deferredSystem = deferredSystem;
            _commandSystem = commandSystem;
            _evaluationSystem = evaluationSystem;
            _eventRouter = eventRouter;
            _reliableOutbox = reliableOutbox;
            _sourceRegistry = sourceRegistry;
            _restoreCoordinator = restoreCoordinator;
        }

        internal RuntimeHost Host => _host;
        internal RuntimeCommandBuffer CommandBuffer => _gameplayModule.CommandBuffer;
        internal GameplayTriggerRuntime TriggerRuntime => _triggerRuntime;
        internal RuntimeCommandSourceRegistry SourceRegistry => _sourceRegistry;
        internal RuntimeCommandSubmissionGate SubmissionGate { get; private set; }
        internal RuntimeHashCombiner HashCombiner { get; private set; }
        internal bool IsReady { get; private set; }

        internal void Bootstrap(
            in GameplayTriggerReplaySessionCreateContext context,
            RuntimeSaveState checkpoint)
        {
            ValidateComposition(in context);
            SubmissionGate = new RuntimeCommandSubmissionGate(
                _sourceRegistry,
                _gameplayModule.CommandBuffer,
                RuntimeCommandIngressKind.External);
            HashCombiner = new RuntimeHashCombiner(new IRuntimeHashContributor[]
            {
                new GameplayTriggerRuntimeHashContributor(_triggerRuntime)
            });

            try
            {
                _host.Initialize();
                if (_host.State != RuntimeLifecycleState.Initialized)
                    throw new InvalidOperationException("RuntimeHost did not reach Initialized state.");
                _host.Start();
                if (_host.State != RuntimeLifecycleState.Started)
                    throw new InvalidOperationException("RuntimeHost did not reach Started state.");
            }
            catch (Exception exception)
            {
                throw new GameplayTriggerReplayException(
                    GameplayTriggerReplayFailureCode.LifecycleFailed,
                    "Gameplay Trigger replay host failed to initialize or start.",
                    innerException: exception);
            }

            if (_host.HasTickAttempt)
            {
                throw new GameplayTriggerReplayException(
                    GameplayTriggerReplayFailureCode.LifecycleFailed,
                    "Gameplay Trigger replay composition attempted to Tick during bootstrap.");
            }

            if (context.BootstrapMode == GameplayTriggerReplayBootstrapMode.CheckpointSuffix)
            {
                RuntimeSaveStateCoordinatorResult<bool> restored =
                    _restoreCoordinator.RestoreSaveState(checkpoint);
                if (!restored.Success || !restored.Value)
                {
                    RuntimeSaveStateError error = restored.FirstError != null
                        ? restored.FirstError.Error
                        : null;
                    throw new GameplayTriggerReplayException(
                        GameplayTriggerReplayFailureCode.CheckpointRestoreFailed,
                        "Gameplay Trigger replay checkpoint restore failed.",
                        checkpointError: error);
                }
            }

            _eventRouter.ResetPresentationState();
            if (_host.HasTickAttempt ||
                _gameplayModule.CommandBuffer.CurrentFrame != context.StartFrame ||
                _gameplayModule.CommandBuffer.PendingCount != 0)
            {
                throw new GameplayTriggerReplayException(
                    GameplayTriggerReplayFailureCode.LifecycleFailed,
                    "Gameplay Trigger replay bootstrap changed Tick or command-buffer state before Ready.");
            }

            IsReady = true;
        }

        private void ValidateComposition(in GameplayTriggerReplaySessionCreateContext context)
        {
            _gameplayRegistered = _host.ContainsModuleInstance(_gameplayModule);
            _reliableRegistered = _reliableOutbox != null &&
                _host.ContainsModuleInstance(_reliableOutbox);

            bool valid = _host.State == RuntimeLifecycleState.Created &&
                _host.ErrorPolicy == RuntimeHostErrorPolicy.FailFast &&
                _gameplayRegistered &&
                string.Equals(
                    _gameplayModule.ModuleId,
                    GameplayRuntimeModule.DefaultModuleId,
                    StringComparison.Ordinal) &&
                _gameplayModule.TickStage == RuntimeTickStage.Simulation &&
                _gameplayModule.Priority == 100 &&
                _gameplayModule.CommandBuffer != null &&
                _gameplayModule.CommandBuffer.CurrentFrame == context.StartFrame &&
                _gameplayModule.CommandBuffer.PendingCount == 0 &&
                _gameplayModule.SystemPipeline != null &&
                _gameplayModule.SystemPipeline.ContainsSystemInstance(_deferredSystem) &&
                _gameplayModule.SystemPipeline.ContainsSystemInstance(_commandSystem) &&
                _gameplayModule.SystemPipeline.ContainsSystemInstance(_evaluationSystem) &&
                _gameplayModule.SystemPipeline.ContainsSystemInstance(_eventRouter) &&
                string.Equals(_deferredSystem.SystemId, GameplayTriggerDeferredSystem.DefaultSystemId, StringComparison.Ordinal) &&
                string.Equals(_commandSystem.SystemId, GameplayTriggerCommandSystem.DefaultSystemId, StringComparison.Ordinal) &&
                string.Equals(_evaluationSystem.SystemId, GameplayTriggerEvaluationSystem.DefaultSystemId, StringComparison.Ordinal) &&
                string.Equals(_eventRouter.SystemId, GameplayTriggerEventRouterSystem.DefaultSystemId, StringComparison.Ordinal) &&
                _deferredSystem.Phase == GameplaySystemPhase.PreCommand &&
                _deferredSystem.Priority == GameplayTriggerDeferredSystem.DefaultPriority &&
                _commandSystem.Phase == GameplaySystemPhase.Command &&
                _commandSystem.Priority == GameplayTriggerCommandSystem.DefaultPriority &&
                _evaluationSystem.Phase == GameplaySystemPhase.Simulation &&
                _evaluationSystem.Priority == GameplayTriggerEvaluationSystem.DefaultPriority &&
                _eventRouter.Phase == GameplaySystemPhase.Diagnostics &&
                _eventRouter.Priority == GameplayTriggerEventRouterSystem.DefaultPriority &&
                _deferredSystem.IsEnabled &&
                _commandSystem.IsEnabled &&
                _evaluationSystem.IsEnabled &&
                ReferenceEquals(_deferredSystem.RuntimeIdentity, _triggerRuntime) &&
                ReferenceEquals(_commandSystem.RuntimeIdentity, _triggerRuntime) &&
                ReferenceEquals(_evaluationSystem.RuntimeIdentity, _triggerRuntime) &&
                _triggerRuntime.AuthorityEventSourceIdentity != null &&
                ReferenceEquals(
                    _eventRouter.EventSourceIdentity,
                    _triggerRuntime.AuthorityEventSourceIdentity) &&
                _commandSystem.MaxSignalCommandItems >= context.MaxSignalCommandItems &&
                _commandSystem.MaxOccupancyCommandItems >= context.MaxOccupancyCommandItems &&
                _commandSystem.MaxScalarCommandItems >= context.MaxScalarCommandItems &&
                _triggerRuntime.AuthorityCatalog.GameplayTriggerCatalogHash ==
                    context.ExpectedGameplayTriggerCatalogHash &&
                _sourceRegistry.IsSealed;

            bool requiresReliable =
                _triggerRuntime.AuthorityCatalog.ReliableDeferredActionCount > 0;
            if (requiresReliable)
            {
                valid = valid && _reliableOutbox != null;
            }

            if (_reliableOutbox != null)
            {
                valid = valid &&
                    _reliableRegistered &&
                    string.Equals(
                        _reliableOutbox.ModuleId,
                        GameplayTriggerReliableOutboxModule.DefaultModuleId,
                        StringComparison.Ordinal) &&
                    _reliableOutbox.TickStage == RuntimeTickStage.PreSimulation &&
                    _reliableOutbox.Priority == GameplayTriggerReliableOutboxModule.DefaultPriority &&
                    _reliableOutbox.IsEnabled &&
                    ReferenceEquals(_reliableOutbox.RuntimeIdentity, _triggerRuntime);
            }

            if (!valid)
            {
                throw new GameplayTriggerReplayException(
                    GameplayTriggerReplayFailureCode.CompositionIdentityMismatch,
                    "Gameplay Trigger replay composition failed exact identity validation.");
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            IsReady = false;
            DisposeComposition(
                _host,
                _gameplayModule,
                _reliableOutbox,
                _gameplayRegistered,
                _reliableRegistered);
        }

        internal static void DisposeComposition(
            RuntimeHost host,
            GameplayRuntimeModule gameplayModule,
            GameplayTriggerReliableOutboxModule reliableOutbox)
        {
            bool gameplayRegistered = host != null &&
                host.ContainsModuleInstance(gameplayModule);
            bool reliableRegistered = host != null && reliableOutbox != null &&
                host.ContainsModuleInstance(reliableOutbox);
            DisposeComposition(
                host,
                gameplayModule,
                reliableOutbox,
                gameplayRegistered,
                reliableRegistered);
        }

        private static void DisposeComposition(
            RuntimeHost host,
            GameplayRuntimeModule gameplayModule,
            GameplayTriggerReliableOutboxModule reliableOutbox,
            bool gameplayRegistered,
            bool reliableRegistered)
        {
            try
            {
                host?.Dispose();
            }
            finally
            {
                try
                {
                    if (!gameplayRegistered)
                        gameplayModule?.Dispose();
                }
                finally
                {
                    if (!reliableRegistered)
                        reliableOutbox?.Dispose();
                }
            }
        }
    }

    /// <summary>Replays trusted exogenous Gameplay Trigger command frames through a full RuntimeHost.</summary>
    public sealed class GameplayTriggerReplayFrameDriver :
        IRuntimeReplayFrameDriver,
        IDisposable
    {
        private readonly IGameplayTriggerReplaySessionFactory _sessionFactory;
        private readonly GameplayTriggerReplayBootstrapOptions _options;
        private readonly GameplayTriggerReplayBootstrapMode _bootstrapMode;
        private readonly RuntimeSaveState _checkpoint;
        private readonly string _expectedConfigHash;
        private readonly bool[] _processedCommandScratch;
        private readonly bool[] _itemSeenScratch;
        private readonly long[] _signalSequenceScratch;
        private readonly int[] _batchSourceBindingScratch;
        private GameplayTriggerReplaySession _session;
        private bool _terminal;
        private bool _disposed;

        /// <remarks>AllocByDesign. Allocates bounded replay admission scratch.</remarks>
        public GameplayTriggerReplayFrameDriver(
            IGameplayTriggerReplaySessionFactory sessionFactory,
            GameplayTriggerReplayBootstrapOptions options,
            GameplayTriggerReplayBootstrapMode bootstrapMode,
            RuntimeSaveState checkpoint = null)
        {
            _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
            if (string.IsNullOrWhiteSpace(options.ExpectedFrameworkVersion) ||
                options.MaxCommandsPerFrame <= 0 ||
                options.MaxSignalCommandItems <= 0 ||
                options.MaxOccupancyCommandItems <= 0 ||
                options.MaxScalarCommandItems <= 0)
            {
                throw new ArgumentException(
                    "Gameplay Trigger replay bootstrap options were not initialized by their public constructor.",
                    nameof(options));
            }
            if (bootstrapMode == GameplayTriggerReplayBootstrapMode.Fresh)
            {
                if (checkpoint != null)
                    throw new ArgumentException("Fresh replay bootstrap cannot accept a checkpoint.", nameof(checkpoint));
            }
            else if (bootstrapMode == GameplayTriggerReplayBootstrapMode.CheckpointSuffix)
            {
                if (checkpoint == null)
                    throw new ArgumentNullException(nameof(checkpoint));
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(bootstrapMode));
            }

            _options = options;
            _bootstrapMode = bootstrapMode;
            _checkpoint = checkpoint;
            _expectedConfigHash = GameplayTriggerReplayHeaderContract.FormatConfigHash(
                options.ExpectedGameplayTriggerCatalogHash);
            _processedCommandScratch = new bool[options.MaxCommandsPerFrame];
            int maxItems = Math.Max(
                options.MaxSignalCommandItems,
                Math.Max(options.MaxOccupancyCommandItems, options.MaxScalarCommandItems));
            _itemSeenScratch = new bool[maxItems];
            _signalSequenceScratch = new long[options.MaxSignalCommandItems];
            _batchSourceBindingScratch = new int[options.MaxCommandsPerFrame];
            NextExpectedFrame = RuntimeFrame.Zero;
        }

        public bool HasActiveSession => _session != null && _session.IsReady;
        public bool IsFaulted { get; private set; }
        public RuntimeFrame NextExpectedFrame { get; private set; }
        public GameplayTriggerReplayFailureCode LastFailureCode { get; private set; }

        /// <remarks>AllocByDesign. Creates, validates, starts, and optionally restores a fresh composition.</remarks>
        public void Reset(RuntimeReplayHeader header)
        {
            ThrowIfDisposed();
            try
            {
                DisposeActiveSession();
            }
            catch (Exception exception)
            {
                IsFaulted = true;
                LastFailureCode = GameplayTriggerReplayFailureCode.LifecycleFailed;
                throw new GameplayTriggerReplayException(
                    GameplayTriggerReplayFailureCode.LifecycleFailed,
                    "Gameplay Trigger replay failed to dispose the previous session.",
                    innerException: exception);
            }
            IsFaulted = false;
            LastFailureCode = GameplayTriggerReplayFailureCode.None;
            _terminal = false;

            try
            {
                ValidateBootstrapHeader(header);
                var context = new GameplayTriggerReplaySessionCreateContext(
                    header,
                    _bootstrapMode,
                    header.StartFrame,
                    _options.ExpectedGameplayTriggerCatalogHash,
                    _expectedConfigHash,
                    _options.MaxCommandsPerFrame,
                    _options.MaxSignalCommandItems,
                    _options.MaxOccupancyCommandItems,
                    _options.MaxScalarCommandItems);
                var builder = new GameplayTriggerReplaySessionBuilder();
                try
                {
                    _sessionFactory.Create(in context, builder);
                }
                catch (Exception exception)
                {
                    try
                    {
                        builder.DisposeTransferredOwnership();
                    }
                    catch
                    {
                        // Preserve the factory failure after best-effort ownership cleanup.
                    }
                    throw new GameplayTriggerReplayException(
                        GameplayTriggerReplayFailureCode.SessionFactoryFailed,
                        "Gameplay Trigger replay session factory failed.",
                        innerException: exception);
                }

                _session = builder.Complete(in context, _checkpoint);
                NextExpectedFrame = header.StartFrame;
            }
            catch (GameplayTriggerReplayException exception)
            {
                Fault(exception.Code);
                throw;
            }
            catch (Exception exception)
            {
                Fault(GameplayTriggerReplayFailureCode.LifecycleFailed);
                throw new GameplayTriggerReplayException(
                    GameplayTriggerReplayFailureCode.LifecycleFailed,
                    "Gameplay Trigger replay bootstrap failed.",
                    innerException: exception);
            }
        }

        /// <remarks>
        /// AllocByDesign overall for snapshot/hash/result. Admission planning uses bounded scratch; the
        /// initialized Host.Tick subregion remains NoAlloc under the supplied factory capacity contract.
        /// </remarks>
        public RuntimeReplayPlaybackFrameResult RunFrame(RuntimeReplayFrameRecord record)
        {
            ThrowIfDisposed();
            if (_session == null || !_session.IsReady || IsFaulted)
                return ThrowFailure(
                    GameplayTriggerReplayFailureCode.InvalidDriverState,
                    "Gameplay Trigger replay driver has no Ready session.");

            try
            {
                if (record == null)
                    return ThrowFailure(
                        GameplayTriggerReplayFailureCode.InvalidDriverState,
                        "Gameplay Trigger replay frame record cannot be null.");
                if (_terminal)
                    return ThrowFailure(
                        GameplayTriggerReplayFailureCode.FrameExhausted,
                        "Gameplay Trigger replay has exhausted the RuntimeFrame domain.",
                        true,
                        record.Frame);
                if (record.Frame != NextExpectedFrame)
                    return ThrowFailure(
                        GameplayTriggerReplayFailureCode.FrameSequenceMismatch,
                        "Gameplay Trigger replay frame does not match the next expected frame.",
                        true,
                        record.Frame);
                if (record.Frame.Value == long.MaxValue)
                    return ThrowFailure(
                        GameplayTriggerReplayFailureCode.FrameExhausted,
                        "RuntimeFrame long.MaxValue cannot be Tick'd by replay playback.",
                        true,
                        record.Frame);

                ValidateFrameCommands(record);
                RuntimeCommandBuffer buffer = _session.CommandBuffer;
                RuntimeCommandBufferSnapshot snapshot = buffer.CaptureSnapshot();
                try
                {
                    for (int commandIndex = 0; commandIndex < record.Commands.Count; commandIndex++)
                    {
                        RuntimeCommandValidationResult submitted =
                            _session.SubmissionGate.Submit(record.Commands[commandIndex]);
                        if (!submitted.Success)
                        {
                            buffer.RestoreSnapshot(snapshot);
                            return ThrowFailure(
                                GameplayTriggerReplayFailureCode.CommandAdmissionFailed,
                                "Gameplay Trigger replay command admission failed.",
                                true,
                                record.Frame,
                                commandIndex);
                        }
                    }
                }
                catch (GameplayTriggerReplayException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    buffer.RestoreSnapshot(snapshot);
                    throw new GameplayTriggerReplayException(
                        GameplayTriggerReplayFailureCode.CommandAdmissionFailed,
                        "Gameplay Trigger replay command admission threw an exception.",
                        true,
                        record.Frame,
                        innerException: exception);
                }

                try
                {
                    _session.Host.Tick(record.Frame.Value, 0d, 0d);
                }
                catch (Exception exception)
                {
                    throw new GameplayTriggerReplayException(
                        GameplayTriggerReplayFailureCode.HostTickFailed,
                        "Gameplay Trigger replay Host.Tick failed.",
                        true,
                        record.Frame,
                        innerException: exception);
                }

                if (!_session.Host.HasTickAttempt ||
                    !_session.Host.LastTickAttemptSucceeded ||
                    _session.Host.LastTickAttemptFrame != record.Frame.Value)
                {
                    return ThrowFailure(
                        GameplayTriggerReplayFailureCode.HostTickFailed,
                        "Gameplay Trigger replay Host.Tick did not complete successfully.",
                        true,
                        record.Frame);
                }

                long actualHash = _session.HashCombiner.ComputeHash(record.Frame);
                var result = new RuntimeReplayPlaybackFrameResult(
                    record.Frame,
                    actualHash,
                    string.Empty);

                if (record.Frame.Value == long.MaxValue - 1L)
                {
                    NextExpectedFrame = new RuntimeFrame(long.MaxValue);
                    _terminal = true;
                }
                else
                {
                    NextExpectedFrame = record.Frame.Next();
                }

                if (actualHash != record.ResultHash)
                    Fault(GameplayTriggerReplayFailureCode.ResultHashMismatch);
                return result;
            }
            catch (GameplayTriggerReplayException exception)
            {
                Fault(exception.Code);
                throw;
            }
            catch (Exception exception)
            {
                Fault(GameplayTriggerReplayFailureCode.HostTickFailed);
                throw new GameplayTriggerReplayException(
                    GameplayTriggerReplayFailureCode.HostTickFailed,
                    "Gameplay Trigger replay frame failed.",
                    record != null,
                    record != null ? record.Frame : default,
                    innerException: exception);
            }
        }

        /// <remarks>LowFreqAlloc lifecycle boundary. Idempotent.</remarks>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            DisposeActiveSession();
        }

        private void ValidateBootstrapHeader(RuntimeReplayHeader header)
        {
            if (header.SchemaVersion != GameplayTriggerReplayHeaderContract.CurrentSchemaVersion ||
                !string.Equals(header.FrameworkVersion, _options.ExpectedFrameworkVersion, StringComparison.Ordinal) ||
                !string.Equals(header.ConfigHash, _expectedConfigHash, StringComparison.Ordinal) ||
                !string.Equals(header.ResourceCatalogHash, _options.ExpectedResourceCatalogHash, StringComparison.Ordinal) ||
                header.StartFrame.Value == long.MaxValue)
            {
                throw new GameplayTriggerReplayException(
                    GameplayTriggerReplayFailureCode.InvalidHeader,
                    "Gameplay Trigger replay header does not match the frozen bootstrap contract.");
            }

            if (_bootstrapMode != GameplayTriggerReplayBootstrapMode.CheckpointSuffix)
                return;

            bool validCheckpoint = _checkpoint.Frame >= 0L &&
                _checkpoint.SchemaVersion == RuntimeSaveState.CurrentSchemaVersion &&
                _checkpoint.Frame < long.MaxValue &&
                _checkpoint.Frame <= long.MaxValue - 2L &&
                _checkpoint.Frame + 1L == header.StartFrame.Value &&
                string.Equals(_checkpoint.FrameworkVersion, _options.ExpectedFrameworkVersion, StringComparison.Ordinal) &&
                string.Equals(_checkpoint.ConfigVersion, _expectedConfigHash, StringComparison.Ordinal) &&
                string.Equals(_checkpoint.ResourceCatalogVersion, _options.ExpectedResourceCatalogHash, StringComparison.Ordinal);
            if (!validCheckpoint)
            {
                throw new GameplayTriggerReplayException(
                    GameplayTriggerReplayFailureCode.InvalidCheckpoint,
                    "Gameplay Trigger replay checkpoint root does not match the frozen F+1/config contract.");
            }
        }

        private void ValidateFrameCommands(RuntimeReplayFrameRecord record)
        {
            IReadOnlyList<RuntimeCommand> commands = record.Commands;
            RuntimeCommandBuffer buffer = _session.CommandBuffer;
            if (commands.Count > _options.MaxCommandsPerFrame ||
                buffer.PendingCount > _options.MaxCommandsPerFrame - commands.Count)
            {
                ThrowFailure(
                    GameplayTriggerReplayFailureCode.CommandCapacityExceeded,
                    "Gameplay Trigger replay frame exceeds its admission command limit.",
                    true,
                    record.Frame);
            }

            for (int commandIndex = 0; commandIndex < commands.Count; commandIndex++)
            {
                RuntimeCommand command = commands[commandIndex];
                if (command.Frame != record.Frame || command.Frame.Value == long.MaxValue)
                {
                    ThrowFailure(
                        GameplayTriggerReplayFailureCode.InvalidCommandPayload,
                        "Gameplay Trigger replay command frame is invalid.",
                        true,
                        record.Frame,
                        commandIndex);
                }

                for (int prior = 0; prior < commandIndex; prior++)
                {
                    if (commands[prior].Sequence == command.Sequence)
                    {
                        ThrowFailure(
                            GameplayTriggerReplayFailureCode.InvalidCommandOrder,
                            "Gameplay Trigger replay command sequence is duplicated within the frame.",
                            true,
                            record.Frame,
                            commandIndex);
                    }
                }

                if (commandIndex > 0 && CompareCommands(commands[commandIndex - 1], command) >= 0)
                {
                    ThrowFailure(
                        GameplayTriggerReplayFailureCode.InvalidCommandOrder,
                        "Gameplay Trigger replay commands are not in strict Runtime drain order.",
                        true,
                        record.Frame,
                        commandIndex);
                }

                ValidateSourceAuthorization(command, record.Frame, commandIndex);
            }

            ValidateTypedBatches(record);
        }

        private void ValidateSourceAuthorization(
            RuntimeCommand command,
            RuntimeFrame frame,
            int commandIndex)
        {
            if (!_session.SourceRegistry.TryGet(
                    command.SourceId,
                    out RuntimeCommandSourceDefinition source) ||
                source.Kind != RuntimeCommandSourceKind.Exogenous ||
                !command.Envelope.IsPresent ||
                command.Envelope.Provenance != RuntimeCommandProvenanceKind.Exogenous ||
                !source.AllowsCommand(command.CommandId))
            {
                ThrowFailure(
                    GameplayTriggerReplayFailureCode.UnauthorizedCommand,
                    "Gameplay Trigger replay command is not authorized as exogenous input.",
                    true,
                    frame,
                    commandIndex);
            }

            if (command.CommandId != GameplayTriggerRuntimeCommandIds.SubmitSignalBatch &&
                command.CommandId != GameplayTriggerRuntimeCommandIds.SubmitOccupancyBatch &&
                command.CommandId != GameplayTriggerRuntimeCommandIds.SubmitScalarBatch)
            {
                ThrowFailure(
                    GameplayTriggerReplayFailureCode.UnauthorizedCommand,
                    "Gameplay Trigger replay accepts only the three typed observation commands.",
                    true,
                    frame,
                    commandIndex);
            }
        }

        private void ValidateTypedBatches(RuntimeReplayFrameRecord record)
        {
            IReadOnlyList<RuntimeCommand> commands = record.Commands;
            Array.Clear(_processedCommandScratch, 0, commands.Count);
            int batchCount = 0;
            int totalItems = 0;

            for (int commandIndex = 0; commandIndex < commands.Count; commandIndex++)
            {
                if (_processedCommandScratch[commandIndex])
                    continue;

                RuntimeCommand command = commands[commandIndex];
                int itemCount;
                GameplayTriggerSourceBindingId sourceBindingId;
                GameplayTriggerSourceKind expectedKind;
                switch (command.CommandId)
                {
                    case GameplayTriggerRuntimeCommandIds.SubmitSignalBatch:
                        itemCount = ValidateSignalBatch(commands, commandIndex, record.Frame, out sourceBindingId);
                        expectedKind = GameplayTriggerSourceKind.Signal;
                        break;
                    case GameplayTriggerRuntimeCommandIds.SubmitOccupancyBatch:
                        itemCount = ValidateOccupancyBatch(commands, commandIndex, record.Frame, out sourceBindingId);
                        expectedKind = GameplayTriggerSourceKind.Occupancy;
                        break;
                    case GameplayTriggerRuntimeCommandIds.SubmitScalarBatch:
                        itemCount = ValidateScalarBatch(commands, commandIndex, record.Frame, out sourceBindingId);
                        expectedKind = GameplayTriggerSourceKind.Threshold;
                        break;
                    default:
                        throw new InvalidOperationException("Command whitelist changed after preflight.");
                }

                for (int priorBatch = 0; priorBatch < batchCount; priorBatch++)
                {
                    if (_batchSourceBindingScratch[priorBatch] == sourceBindingId.Value)
                    {
                        ThrowFailure(
                            GameplayTriggerReplayFailureCode.InvalidCommandPayload,
                            "Gameplay Trigger replay contains duplicate source submissions in one frame.",
                            true,
                            record.Frame,
                            commandIndex);
                    }
                }

                if (!_session.TriggerRuntime.TryGetSourceKind(sourceBindingId, out GameplayTriggerSourceKind actualKind) ||
                    actualKind != expectedKind)
                {
                    ThrowFailure(
                        GameplayTriggerReplayFailureCode.InvalidCommandPayload,
                        "Gameplay Trigger replay source binding is unknown or has the wrong reducer kind.",
                        true,
                        record.Frame,
                        commandIndex);
                }

                _batchSourceBindingScratch[batchCount] = sourceBindingId.Value;
                batchCount++;
                if (itemCount > int.MaxValue - totalItems)
                    ThrowCapacity(record.Frame, commandIndex, "Gameplay Trigger replay item count overflowed.");
                totalItems += itemCount;
            }

            GameplayTriggerRuntimeOptions runtimeOptions =
                _session.TriggerRuntime.AuthorityOptions;
            if (batchCount > runtimeOptions.MaxInputBatches ||
                totalItems > runtimeOptions.MaxInputItems)
            {
                ThrowCapacity(
                    record.Frame,
                    commands.Count == 0 ? -1 : commands.Count - 1,
                    "Gameplay Trigger replay exceeds runtime input capacity.");
            }
        }

        private int ValidateSignalBatch(
            IReadOnlyList<RuntimeCommand> commands,
            int firstIndex,
            RuntimeFrame frame,
            out GameplayTriggerSourceBindingId sourceBindingId)
        {
            RuntimeCommand firstCommand = commands[firstIndex];
            if (!GameplayTriggerSignalCommandCodec.TryDecodeRecord(
                    firstCommand,
                    out GameplayTriggerSignalCommandRecord first,
                    out _))
            {
                sourceBindingId = default;
                return ThrowPayload(frame, firstIndex, "Signal replay record is malformed.");
            }

            sourceBindingId = first.SourceBindingId;
            int itemCount = first.ItemCount;
            if (itemCount > _options.MaxSignalCommandItems)
                return ThrowCapacity(frame, firstIndex, "Signal replay batch exceeds its fixed capacity.");
            Array.Clear(_itemSeenScratch, 0, itemCount);
            int matched = 0;

            for (int index = firstIndex; index < commands.Count; index++)
            {
                RuntimeCommand candidate = commands[index];
                if (_processedCommandScratch[index] ||
                    !GameplayTriggerSignalCommandCodec.HasRawBatchIdentity(candidate, firstCommand))
                    continue;
                if (!GameplayTriggerSignalCommandCodec.TryDecodeRecord(
                        candidate,
                        out GameplayTriggerSignalCommandRecord decoded,
                        out _) ||
                    decoded.ItemCount != itemCount ||
                    decoded.Ordering != first.Ordering ||
                    decoded.Provenance != first.Provenance)
                    return ThrowPayload(frame, index, "Signal replay batch metadata is inconsistent.");

                _processedCommandScratch[index] = true;
                matched++;
                if (itemCount == 0)
                    continue;
                if (_itemSeenScratch[decoded.ItemIndex])
                    return ThrowPayload(frame, index, "Signal replay batch contains a duplicate item index.");
                _itemSeenScratch[decoded.ItemIndex] = true;
                _signalSequenceScratch[decoded.ItemIndex] = decoded.Signal.Sequence;
            }

            ValidateBatchCompleteness(frame, firstIndex, itemCount, matched);
            if (first.Ordering == GameplayTriggerSignalOrdering.Ordered)
            {
                for (int itemIndex = 1; itemIndex < itemCount; itemIndex++)
                {
                    if (_signalSequenceScratch[itemIndex] <= _signalSequenceScratch[itemIndex - 1])
                        return ThrowPayload(frame, firstIndex, "Ordered Signal replay sequence is not strictly increasing.");
                }
            }
            return itemCount;
        }

        private int ValidateOccupancyBatch(
            IReadOnlyList<RuntimeCommand> commands,
            int firstIndex,
            RuntimeFrame frame,
            out GameplayTriggerSourceBindingId sourceBindingId)
        {
            RuntimeCommand firstCommand = commands[firstIndex];
            if (!GameplayTriggerOccupancyCommandCodec.TryDecodeRecord(
                    firstCommand,
                    out GameplayTriggerOccupancyCommandRecord first,
                    out _))
            {
                sourceBindingId = default;
                return ThrowPayload(frame, firstIndex, "Occupancy replay record is malformed.");
            }

            sourceBindingId = first.SourceBindingId;
            int itemCount = first.ItemCount;
            if (itemCount > _options.MaxOccupancyCommandItems)
                return ThrowCapacity(frame, firstIndex, "Occupancy replay batch exceeds its fixed capacity.");
            Array.Clear(_itemSeenScratch, 0, itemCount);
            int matched = 0;
            for (int index = firstIndex; index < commands.Count; index++)
            {
                RuntimeCommand candidate = commands[index];
                if (_processedCommandScratch[index] ||
                    !GameplayTriggerOccupancyCommandCodec.HasRawBatchIdentity(candidate, firstCommand))
                    continue;
                if (!GameplayTriggerOccupancyCommandCodec.TryDecodeRecord(
                        candidate,
                        out GameplayTriggerOccupancyCommandRecord decoded,
                        out _) ||
                    decoded.ItemCount != itemCount ||
                    decoded.Provenance != first.Provenance)
                    return ThrowPayload(frame, index, "Occupancy replay batch metadata is inconsistent.");

                _processedCommandScratch[index] = true;
                matched++;
                if (itemCount == 0)
                    continue;
                if (_itemSeenScratch[decoded.ItemIndex])
                    return ThrowPayload(frame, index, "Occupancy replay batch contains a duplicate item index.");
                _itemSeenScratch[decoded.ItemIndex] = true;
            }

            ValidateBatchCompleteness(frame, firstIndex, itemCount, matched);
            return itemCount;
        }

        private int ValidateScalarBatch(
            IReadOnlyList<RuntimeCommand> commands,
            int firstIndex,
            RuntimeFrame frame,
            out GameplayTriggerSourceBindingId sourceBindingId)
        {
            RuntimeCommand firstCommand = commands[firstIndex];
            if (!GameplayTriggerScalarCommandCodec.TryDecodeRecord(
                    firstCommand,
                    out GameplayTriggerScalarCommandRecord first,
                    out _))
            {
                sourceBindingId = default;
                return ThrowPayload(frame, firstIndex, "Scalar replay record is malformed.");
            }

            sourceBindingId = first.SourceBindingId;
            int itemCount = first.ItemCount;
            if (itemCount > _options.MaxScalarCommandItems)
                return ThrowCapacity(frame, firstIndex, "Scalar replay batch exceeds its fixed capacity.");
            Array.Clear(_itemSeenScratch, 0, itemCount);
            int matched = 0;
            for (int index = firstIndex; index < commands.Count; index++)
            {
                RuntimeCommand candidate = commands[index];
                if (_processedCommandScratch[index] ||
                    !GameplayTriggerScalarCommandCodec.HasRawBatchIdentity(candidate, firstCommand))
                    continue;
                if (!GameplayTriggerScalarCommandCodec.TryDecodeRecord(
                        candidate,
                        out GameplayTriggerScalarCommandRecord decoded,
                        out _) ||
                    decoded.ItemCount != itemCount ||
                    decoded.Provenance != first.Provenance)
                    return ThrowPayload(frame, index, "Scalar replay batch metadata is inconsistent.");

                _processedCommandScratch[index] = true;
                matched++;
                if (itemCount == 0)
                    continue;
                if (_itemSeenScratch[decoded.ItemIndex])
                    return ThrowPayload(frame, index, "Scalar replay batch contains a duplicate item index.");
                _itemSeenScratch[decoded.ItemIndex] = true;
            }

            ValidateBatchCompleteness(frame, firstIndex, itemCount, matched);
            return itemCount;
        }

        private void ValidateBatchCompleteness(
            RuntimeFrame frame,
            int commandIndex,
            int itemCount,
            int matched)
        {
            int expected = itemCount == 0 ? 1 : itemCount;
            if (matched != expected)
                ThrowPayload(frame, commandIndex, "Gameplay Trigger replay batch is incomplete.");
            for (int itemIndex = 0; itemIndex < itemCount; itemIndex++)
            {
                if (!_itemSeenScratch[itemIndex])
                    ThrowPayload(frame, commandIndex, "Gameplay Trigger replay batch has a missing item index.");
            }
        }

        private static int CompareCommands(RuntimeCommand left, RuntimeCommand right)
        {
            int frame = left.Frame.CompareTo(right.Frame);
            if (frame != 0) return frame;
            int source = left.SourceId.CompareTo(right.SourceId);
            if (source != 0) return source;
            int command = left.CommandId.CompareTo(right.CommandId);
            if (command != 0) return command;
            int target = left.TargetId.CompareTo(right.TargetId);
            if (target != 0) return target;
            return left.Sequence.CompareTo(right.Sequence);
        }

        private int ThrowPayload(RuntimeFrame frame, int commandIndex, string message)
        {
            return ThrowFailure<int>(
                GameplayTriggerReplayFailureCode.InvalidCommandPayload,
                message,
                true,
                frame,
                commandIndex);
        }

        private int ThrowCapacity(RuntimeFrame frame, int commandIndex, string message)
        {
            return ThrowFailure<int>(
                GameplayTriggerReplayFailureCode.CommandCapacityExceeded,
                message,
                true,
                frame,
                commandIndex);
        }

        private RuntimeReplayPlaybackFrameResult ThrowFailure(
            GameplayTriggerReplayFailureCode code,
            string message,
            bool hasFrame = false,
            RuntimeFrame frame = default,
            int commandIndex = -1)
        {
            return ThrowFailure<RuntimeReplayPlaybackFrameResult>(
                code,
                message,
                hasFrame,
                frame,
                commandIndex);
        }

        private T ThrowFailure<T>(
            GameplayTriggerReplayFailureCode code,
            string message,
            bool hasFrame = false,
            RuntimeFrame frame = default,
            int commandIndex = -1)
        {
            throw new GameplayTriggerReplayException(
                code,
                message,
                hasFrame,
                frame,
                commandIndex);
        }

        private void Fault(GameplayTriggerReplayFailureCode code)
        {
            LastFailureCode = code;
            IsFaulted = true;
            try
            {
                DisposeActiveSession();
            }
            catch
            {
                // Fault retains its original typed cause after best-effort ownership cleanup.
            }
        }

        private void DisposeActiveSession()
        {
            GameplayTriggerReplaySession session = _session;
            _session = null;
            session?.Dispose();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(GameplayTriggerReplayFrameDriver));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MxFramework.Runtime
{
    public readonly struct RuntimeCommandDefinition
    {
        public RuntimeCommandDefinition(
            int commandId,
            string name,
            RuntimeCommandPayloadSchema payloadSchema = null,
            string description = "")
        {
            CommandId = commandId;
            Name = name ?? string.Empty;
            PayloadSchema = payloadSchema;
            Description = description ?? string.Empty;
        }

        public int CommandId { get; }
        public string Name { get; }
        public RuntimeCommandPayloadSchema PayloadSchema { get; }
        public string Description { get; }
        public bool HasPayloadSchema => PayloadSchema != null;
    }

    public sealed class RuntimeCommandPayloadSchema
    {
        private readonly Func<RuntimeCommand, bool> _validator;

        public RuntimeCommandPayloadSchema(string description)
            : this(description, null)
        {
        }

        public RuntimeCommandPayloadSchema(string description, Func<RuntimeCommand, bool> validator)
        {
            Description = description ?? string.Empty;
            _validator = validator;
        }

        public string Description { get; }

        public bool IsValid(RuntimeCommand command)
        {
            return _validator == null || _validator(command);
        }
    }

    public sealed class RuntimeCommandRegistry
    {
        private readonly Dictionary<int, RuntimeCommandDefinition> _definitions = new Dictionary<int, RuntimeCommandDefinition>();

        public int Count => _definitions.Count;

        public void Register(RuntimeCommandDefinition definition)
        {
            ValidateDefinition(definition);

            if (_definitions.ContainsKey(definition.CommandId))
            {
                throw new ArgumentException("Runtime command id is already registered: " + definition.CommandId + ".", nameof(definition));
            }

            _definitions.Add(definition.CommandId, definition);
        }

        public bool TryGet(int commandId, out RuntimeCommandDefinition definition)
        {
            return _definitions.TryGetValue(commandId, out definition);
        }

        public IReadOnlyList<RuntimeCommandDefinition> ListDefinitions()
        {
            return CreateSortedDefinitions();
        }

        public RuntimeCommandRegistrySnapshot CreateSnapshot()
        {
            return new RuntimeCommandRegistrySnapshot(CreateSortedDefinitions());
        }

        private static void ValidateDefinition(RuntimeCommandDefinition definition)
        {
            if (definition.CommandId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(definition), "Runtime command id cannot be negative.");
            }

            if (string.IsNullOrWhiteSpace(definition.Name))
            {
                throw new ArgumentException("Runtime command name cannot be null, empty, or whitespace.", nameof(definition));
            }
        }

        private IReadOnlyList<RuntimeCommandDefinition> CreateSortedDefinitions()
        {
            if (_definitions.Count == 0)
            {
                return RuntimeCommandRegistrySnapshot.Empty.Definitions;
            }

            var definitions = new List<RuntimeCommandDefinition>(_definitions.Count);
            foreach (KeyValuePair<int, RuntimeCommandDefinition> pair in _definitions)
            {
                definitions.Add(pair.Value);
            }

            definitions.Sort(CompareDefinitions);
            return new ReadOnlyCollection<RuntimeCommandDefinition>(definitions);
        }

        private static int CompareDefinitions(RuntimeCommandDefinition left, RuntimeCommandDefinition right)
        {
            int id = left.CommandId.CompareTo(right.CommandId);
            if (id != 0)
            {
                return id;
            }

            return string.Compare(left.Name, right.Name, StringComparison.Ordinal);
        }
    }

    public readonly struct RuntimeCommandRegistrySnapshot
    {
        public static readonly RuntimeCommandRegistrySnapshot Empty =
            new RuntimeCommandRegistrySnapshot(new ReadOnlyCollection<RuntimeCommandDefinition>(Array.Empty<RuntimeCommandDefinition>()));

        public RuntimeCommandRegistrySnapshot(IReadOnlyList<RuntimeCommandDefinition> definitions)
        {
            Definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
        }

        public IReadOnlyList<RuntimeCommandDefinition> Definitions { get; }
    }

    public sealed class RuntimeCommandRegistryValidator : IRuntimeCommandValidator
    {
        private readonly RuntimeCommandRegistry _registry;

        public RuntimeCommandRegistryValidator(RuntimeCommandRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public RuntimeCommandValidationResult Validate(RuntimeCommand command)
        {
            if (command.CommandId < 0)
            {
                return RuntimeCommandValidationResult.Failed(new RuntimeCommandError(
                    RuntimeCommandErrorCode.InvalidCommandId,
                    command,
                    RuntimeFrame.Zero,
                    "Runtime command id cannot be negative."));
            }

            RuntimeCommandDefinition definition;
            if (!_registry.TryGet(command.CommandId, out definition))
            {
                return RuntimeCommandValidationResult.Failed(new RuntimeCommandError(
                    RuntimeCommandErrorCode.UnregisteredCommandId,
                    command,
                    RuntimeFrame.Zero,
                    "Runtime command id is not registered: " + command.CommandId + "."));
            }

            if (definition.PayloadSchema != null && !definition.PayloadSchema.IsValid(command))
            {
                return RuntimeCommandValidationResult.Failed(new RuntimeCommandError(
                    RuntimeCommandErrorCode.InvalidPayload,
                    command,
                    RuntimeFrame.Zero,
                    "Runtime command payload is invalid for command '" + definition.Name + "'."));
            }

            return RuntimeCommandValidationResult.Accepted(command);
        }
    }

    /// <summary>Classifies the deterministic origin of commands admitted by a trusted composition root.</summary>
    public enum RuntimeCommandSourceKind
    {
        /// <summary>Reserved compatibility classification for SourceId 0 outside the trusted registry.</summary>
        LegacyUnspecified = 0,
        Exogenous = 1,
        Derived = 2
    }

    /// <summary>Identifies which trusted edge is submitting a command.</summary>
    public enum RuntimeCommandIngressKind
    {
        External = 1,
        TrustedDerived = 2
    }

    public enum RuntimeCommandSourceRegistrationErrorCode
    {
        None = 0,
        InvalidSourceId = 1,
        InvalidSourceKind = 2,
        DuplicateSourceId = 3,
        InvalidCommandId = 4,
        DuplicateCommandId = 5,
        RegistrySealed = 6
    }

    /// <summary>Structured initialization result for command-source registration.</summary>
    public readonly struct RuntimeCommandSourceRegistrationResult
    {
        internal RuntimeCommandSourceRegistrationResult(
            bool success,
            RuntimeCommandSourceRegistrationErrorCode errorCode,
            int sourceId,
            int relatedValue)
        {
            Success = success;
            ErrorCode = errorCode;
            SourceId = sourceId;
            RelatedValue = relatedValue;
        }

        public bool Success { get; }
        public RuntimeCommandSourceRegistrationErrorCode ErrorCode { get; }
        public int SourceId { get; }
        public int RelatedValue { get; }
    }

    /// <summary>Immutable command-source metadata returned by the trusted source registry.</summary>
    public readonly struct RuntimeCommandSourceDefinition
    {
        private readonly int[] _allowedCommandIds;

        internal RuntimeCommandSourceDefinition(
            int sourceId,
            RuntimeCommandSourceKind kind,
            int[] allowedCommandIds)
        {
            SourceId = sourceId;
            Kind = kind;
            _allowedCommandIds = allowedCommandIds;
        }

        public int SourceId { get; }
        public RuntimeCommandSourceKind Kind { get; }
        public int AllowedCommandCount => _allowedCommandIds == null ? 0 : _allowedCommandIds.Length;

        /// <summary>Returns whether this source explicitly allows the command id.</summary>
        /// <remarks>NoAlloc.</remarks>
        public bool AllowsCommand(int commandId)
        {
            return _allowedCommandIds != null &&
                Array.BinarySearch(_allowedCommandIds, commandId) >= 0;
        }
    }

    /// <summary>
    /// Initialization-time registry for stable command sources and their explicit command allowlists.
    /// </summary>
    /// <remarks>
    /// TryRegister is AllocByDesign. Seal and all lookup/authorization paths are NoAlloc after initialization.
    /// SourceId 0 is intentionally not registrable and remains the legacy/unspecified compatibility path.
    /// </remarks>
    public sealed class RuntimeCommandSourceRegistry
    {
        public const int LegacyUnspecifiedSourceId = 0;

        private readonly Dictionary<int, RuntimeCommandSourceDefinition> _definitions;

        /// <summary>Creates an initialization-time registry with optional source capacity.</summary>
        /// <remarks>AllocByDesign during composition-root initialization.</remarks>
        public RuntimeCommandSourceRegistry(int initialCapacity = 0)
        {
            if (initialCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(initialCapacity));

            _definitions = new Dictionary<int, RuntimeCommandSourceDefinition>(initialCapacity);
        }

        public int Count => _definitions.Count;
        public bool IsSealed { get; private set; }

        /// <summary>Copies and freezes one source-specific command allowlist.</summary>
        /// <remarks>AllocByDesign during composition-root initialization.</remarks>
        public bool TryRegister(
            int sourceId,
            RuntimeCommandSourceKind kind,
            ReadOnlySpan<int> allowedCommandIds,
            out RuntimeCommandSourceRegistrationResult result)
        {
            if (IsSealed)
            {
                result = Failed(
                    RuntimeCommandSourceRegistrationErrorCode.RegistrySealed,
                    sourceId,
                    relatedValue: 0);
                return false;
            }
            if (sourceId <= LegacyUnspecifiedSourceId)
            {
                result = Failed(
                    RuntimeCommandSourceRegistrationErrorCode.InvalidSourceId,
                    sourceId,
                    sourceId);
                return false;
            }
            if (kind != RuntimeCommandSourceKind.Exogenous &&
                kind != RuntimeCommandSourceKind.Derived)
            {
                result = Failed(
                    RuntimeCommandSourceRegistrationErrorCode.InvalidSourceKind,
                    sourceId,
                    (int)kind);
                return false;
            }
            if (_definitions.ContainsKey(sourceId))
            {
                result = Failed(
                    RuntimeCommandSourceRegistrationErrorCode.DuplicateSourceId,
                    sourceId,
                    sourceId);
                return false;
            }

            int[] commandIds = new int[allowedCommandIds.Length];
            for (int i = 0; i < allowedCommandIds.Length; i++)
            {
                int commandId = allowedCommandIds[i];
                if (commandId < 0)
                {
                    result = Failed(
                        RuntimeCommandSourceRegistrationErrorCode.InvalidCommandId,
                        sourceId,
                        commandId);
                    return false;
                }
                commandIds[i] = commandId;
            }

            Array.Sort(commandIds);
            for (int i = 1; i < commandIds.Length; i++)
            {
                if (commandIds[i] == commandIds[i - 1])
                {
                    result = Failed(
                        RuntimeCommandSourceRegistrationErrorCode.DuplicateCommandId,
                        sourceId,
                        commandIds[i]);
                    return false;
                }
            }

            _definitions.Add(
                sourceId,
                new RuntimeCommandSourceDefinition(sourceId, kind, commandIds));
            result = new RuntimeCommandSourceRegistrationResult(
                true,
                RuntimeCommandSourceRegistrationErrorCode.None,
                sourceId,
                relatedValue: 0);
            return true;
        }

        /// <remarks>NoAlloc.</remarks>
        public void Seal()
        {
            IsSealed = true;
        }

        /// <remarks>NoAlloc after initialization.</remarks>
        public bool TryGet(int sourceId, out RuntimeCommandSourceDefinition definition)
        {
            return _definitions.TryGetValue(sourceId, out definition);
        }

        /// <remarks>NoAlloc after initialization.</remarks>
        internal bool IsAuthorizedExogenous(RuntimeCommand command)
        {
            return _definitions.TryGetValue(command.SourceId, out RuntimeCommandSourceDefinition definition)
                && definition.Kind == RuntimeCommandSourceKind.Exogenous
                && command.Envelope.IsPresent
                && command.Envelope.Provenance == RuntimeCommandProvenanceKind.Exogenous
                && definition.AllowsCommand(command.CommandId);
        }

        private static RuntimeCommandSourceRegistrationResult Failed(
            RuntimeCommandSourceRegistrationErrorCode errorCode,
            int sourceId,
            int relatedValue)
        {
            return new RuntimeCommandSourceRegistrationResult(
                false,
                errorCode,
                sourceId,
                relatedValue);
        }
    }

    /// <summary>
    /// Opt-in trusted submission boundary that authorizes a fixed ingress before using RuntimeCommandBuffer.
    /// </summary>
    /// <remarks>
    /// Submit is NoAlloc after initialization while the target buffer remains within its configured capacity.
    /// Numeric SourceId is metadata, not authentication; composition roots must expose only the intended gate.
    /// Direct RuntimeCommandBuffer.Enqueue remains the legacy/local compatibility path and is not an authorization boundary.
    /// </remarks>
    public sealed class RuntimeCommandSubmissionGate
    {
        private const string UnknownSourceMessage = "Runtime command source is not registered at this trusted boundary.";
        private const string UnauthorizedSourceMessage = "Runtime command source classification is not authorized for this ingress.";
        private const string ProvenanceMessage = "Runtime command envelope provenance does not match its registered source and ingress.";
        private const string UnauthorizedCommandMessage = "Runtime command id is not in the registered source allowlist.";
        private const string AtomicBatchRequiresBoundedBufferMessage =
            "Atomic Runtime command batch admission requires a hard-bounded target buffer.";

        private readonly RuntimeCommandSourceRegistry _sourceRegistry;
        private readonly RuntimeCommandBuffer _commandBuffer;

        /// <summary>Creates and seals a fixed-ingress submission boundary.</summary>
        /// <remarks>AllocByDesign during composition-root initialization.</remarks>
        public RuntimeCommandSubmissionGate(
            RuntimeCommandSourceRegistry sourceRegistry,
            RuntimeCommandBuffer commandBuffer,
            RuntimeCommandIngressKind ingressKind)
        {
            _sourceRegistry = sourceRegistry ?? throw new ArgumentNullException(nameof(sourceRegistry));
            _commandBuffer = commandBuffer ?? throw new ArgumentNullException(nameof(commandBuffer));
            if (ingressKind != RuntimeCommandIngressKind.External &&
                ingressKind != RuntimeCommandIngressKind.TrustedDerived)
            {
                throw new ArgumentOutOfRangeException(nameof(ingressKind));
            }

            IngressKind = ingressKind;
            _sourceRegistry.Seal();
        }

        public RuntimeCommandIngressKind IngressKind { get; }

        /// <summary>Gets whether the target command buffer has an immutable hard pending limit.</summary>
        /// <remarks>NoAlloc.</remarks>
        public bool IsTargetPendingCapacityBounded =>
            _commandBuffer.IsPendingCapacityBounded;

        /// <summary>Gets the target hard pending limit, or -1 for a legacy unbounded buffer.</summary>
        /// <remarks>NoAlloc.</remarks>
        public int TargetPendingCapacityLimit =>
            _commandBuffer.PendingCapacityLimit;

        /// <remarks>NoAlloc. Internal exact-composition identity seam.</remarks>
        internal RuntimeCommandBuffer CommandBufferIdentity => _commandBuffer;

        /// <remarks>NoAlloc. Internal exact-composition target-validator identity.</remarks>
        internal IRuntimeCommandValidator TargetValidatorIdentity =>
            _commandBuffer.ValidatorIdentity;

        /// <remarks>NoAlloc. Internal exact-composition and source-authorization seam.</remarks>
        internal RuntimeCommandSourceRegistry SourceRegistryIdentity => _sourceRegistry;

        /// <remarks>NoAlloc after initialization while the command buffer remains within capacity.</remarks>
        public RuntimeCommandValidationResult Submit(RuntimeCommand command)
        {
            RuntimeCommandValidationResult authorization = Authorize(command);
            if (!authorization.Success)
                return authorization;

            return _commandBuffer.Enqueue(command);
        }

        /// <summary>Atomically authorizes and admits a caller-owned command span.</summary>
        /// <remarks>
        /// NoAlloc. Only hard-bounded target buffers are supported. Every command is preflighted in caller
        /// order before a no-fail commit; any failure leaves pending commands, current frame and next sequence
        /// unchanged. An empty span succeeds only when the target is hard-bounded.
        /// </remarks>
        public RuntimeCommandBatchSubmissionResult SubmitBatch(
            ReadOnlySpan<RuntimeCommand> commands)
        {
            if (!_commandBuffer.IsPendingCapacityBounded)
            {
                return RuntimeCommandBatchSubmissionResult.Failed(
                    failedCommandIndex: -1,
                    new RuntimeCommandError(
                        RuntimeCommandErrorCode.AtomicBatchRequiresBoundedBuffer,
                        default,
                        _commandBuffer.CurrentFrame,
                        AtomicBatchRequiresBoundedBufferMessage));
            }

            if (commands.Length == 0)
                return RuntimeCommandBatchSubmissionResult.Accepted(submittedCount: 0);

            for (int i = 0; i < commands.Length; i++)
            {
                RuntimeCommand command = commands[i];
                RuntimeCommandValidationResult authorization = Authorize(command);
                if (!authorization.Success)
                {
                    return RuntimeCommandBatchSubmissionResult.Failed(
                        i,
                        authorization.Error);
                }

                RuntimeCommandValidationResult validation =
                    _commandBuffer.ValidateAtomicBatchItem(command, i);
                if (!validation.Success)
                {
                    return RuntimeCommandBatchSubmissionResult.Failed(
                        i,
                        validation.Error);
                }
            }

            _commandBuffer.CommitAtomicBatch(commands);
            return RuntimeCommandBatchSubmissionResult.Accepted(commands.Length);
        }

        private RuntimeCommandValidationResult Authorize(RuntimeCommand command)
        {
            if (!_sourceRegistry.TryGet(command.SourceId, out RuntimeCommandSourceDefinition source))
            {
                return Rejected(
                    RuntimeCommandErrorCode.UnregisteredSourceId,
                    command,
                    UnknownSourceMessage);
            }

            RuntimeCommandSourceKind expectedSourceKind =
                IngressKind == RuntimeCommandIngressKind.External
                    ? RuntimeCommandSourceKind.Exogenous
                    : RuntimeCommandSourceKind.Derived;
            if (source.Kind != expectedSourceKind)
            {
                return Rejected(
                    RuntimeCommandErrorCode.UnauthorizedSource,
                    command,
                    UnauthorizedSourceMessage);
            }

            RuntimeCommandProvenanceKind expectedProvenance =
                IngressKind == RuntimeCommandIngressKind.External
                    ? RuntimeCommandProvenanceKind.Exogenous
                    : RuntimeCommandProvenanceKind.Derived;
            if (!command.Envelope.IsPresent ||
                command.Envelope.Provenance != expectedProvenance)
            {
                return Rejected(
                    RuntimeCommandErrorCode.ProvenanceMismatch,
                    command,
                    ProvenanceMessage);
            }

            if (!source.AllowsCommand(command.CommandId))
            {
                return Rejected(
                    RuntimeCommandErrorCode.UnauthorizedCommand,
                    command,
                    UnauthorizedCommandMessage);
            }

            return RuntimeCommandValidationResult.Accepted(command);
        }

        private RuntimeCommandValidationResult Rejected(
            RuntimeCommandErrorCode errorCode,
            RuntimeCommand command,
            string message)
        {
            return RuntimeCommandValidationResult.Failed(new RuntimeCommandError(
                errorCode,
                command,
                _commandBuffer.CurrentFrame,
                message));
        }
    }
}

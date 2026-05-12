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
}

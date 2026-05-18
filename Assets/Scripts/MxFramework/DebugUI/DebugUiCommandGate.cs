using System;
using System.Collections.Generic;
using System.Text;
using MxFramework.Diagnostics;

namespace MxFramework.DebugUI
{
    public enum DebugUiCommandRisk
    {
        Low = 0,
        Medium = 1,
        Destructive = 2
    }

    public enum DebugUiCommandParameterType
    {
        String = 0,
        Integer = 1,
        Float = 2,
        Boolean = 3
    }

    public readonly struct DebugUiCommandParameterDescriptor
    {
        public DebugUiCommandParameterDescriptor(
            string name,
            DebugUiCommandParameterType type,
            bool required = false,
            string displayName = null)
        {
            Name = name ?? string.Empty;
            Type = type;
            Required = required;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? Name : displayName;
        }

        public string Name { get; }
        public DebugUiCommandParameterType Type { get; }
        public bool Required { get; }
        public string DisplayName { get; }
    }

    public sealed class DebugUiCommandDescriptor
    {
        private readonly List<DebugUiCommandParameterDescriptor> _parameters;

        public DebugUiCommandDescriptor(
            string commandId,
            string displayName,
            string description,
            DebugUiCommandRisk risk,
            bool requiresConfirmation,
            IReadOnlyList<DebugUiCommandParameterDescriptor> parameters = null)
        {
            if (string.IsNullOrWhiteSpace(commandId))
                throw new ArgumentException("Debug UI command id cannot be empty.", nameof(commandId));

            CommandId = commandId;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? commandId : displayName;
            Description = description ?? string.Empty;
            Risk = risk;
            RequiresConfirmation = requiresConfirmation;
            _parameters = parameters != null
                ? new List<DebugUiCommandParameterDescriptor>(parameters)
                : new List<DebugUiCommandParameterDescriptor>();
        }

        public string CommandId { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public DebugUiCommandRisk Risk { get; }
        public bool RequiresConfirmation { get; }
        public IReadOnlyList<DebugUiCommandParameterDescriptor> Parameters => _parameters;
    }

    public sealed class DebugUiCommandRequest
    {
        public DebugUiCommandRequest(
            string commandId,
            bool confirmed = false,
            IReadOnlyDictionary<string, string> arguments = null,
            string traceId = null)
        {
            CommandId = commandId ?? string.Empty;
            Confirmed = confirmed;
            Arguments = arguments ?? EmptyArguments.Instance;
            TraceId = traceId ?? string.Empty;
        }

        public string CommandId { get; }
        public bool Confirmed { get; }
        public IReadOnlyDictionary<string, string> Arguments { get; }
        public string TraceId { get; }

        private sealed class EmptyArguments : IReadOnlyDictionary<string, string>
        {
            public static readonly EmptyArguments Instance = new EmptyArguments();

            public IEnumerable<string> Keys => Array.Empty<string>();
            public IEnumerable<string> Values => Array.Empty<string>();
            public int Count => 0;
            public string this[string key] => throw new KeyNotFoundException(key);

            public bool ContainsKey(string key)
            {
                return false;
            }

            public bool TryGetValue(string key, out string value)
            {
                value = null;
                return false;
            }

            public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
            {
                yield break;
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }
    }

    public sealed class DebugUiCommandResult
    {
        private DebugUiCommandResult(
            string commandId,
            bool success,
            string message,
            string errorCode,
            string traceId)
        {
            CommandId = commandId ?? string.Empty;
            Success = success;
            Message = message ?? string.Empty;
            ErrorCode = errorCode ?? string.Empty;
            TraceId = traceId ?? string.Empty;
        }

        public string CommandId { get; }
        public bool Success { get; }
        public string Message { get; }
        public string ErrorCode { get; }
        public string TraceId { get; }

        public static DebugUiCommandResult Succeeded(string commandId, string message = "", string traceId = "")
        {
            return new DebugUiCommandResult(commandId, success: true, message, string.Empty, traceId);
        }

        public static DebugUiCommandResult Failed(string commandId, string errorCode, string message, string traceId = "")
        {
            return new DebugUiCommandResult(commandId, success: false, message, errorCode, traceId);
        }
    }

    public interface IDebugUiCommandProvider
    {
        IReadOnlyList<DebugUiCommandDescriptor> Commands { get; }
        bool TryGetCommand(string commandId, out DebugUiCommandDescriptor descriptor);
        DebugUiCommandResult Execute(DebugUiCommandRequest request);
    }

    public sealed class EmptyDebugUiCommandProvider : IDebugUiCommandProvider
    {
        public static readonly EmptyDebugUiCommandProvider Instance = new EmptyDebugUiCommandProvider();

        private EmptyDebugUiCommandProvider()
        {
        }

        public IReadOnlyList<DebugUiCommandDescriptor> Commands => Array.Empty<DebugUiCommandDescriptor>();

        public bool TryGetCommand(string commandId, out DebugUiCommandDescriptor descriptor)
        {
            descriptor = null;
            return false;
        }

        public DebugUiCommandResult Execute(DebugUiCommandRequest request)
        {
            string commandId = request != null ? request.CommandId : string.Empty;
            return DebugUiCommandResult.Failed(commandId, "no_provider", "No Debug UI command provider is registered.");
        }
    }

    public sealed class DebugUiCommandGateOptions
    {
        public bool Enabled { get; set; }
        public bool AllowDestructiveCommands { get; set; }
    }

    public readonly struct DebugUiCommandLogEntry
    {
        public DebugUiCommandLogEntry(long sequence, DebugUiCommandRequest request, DebugUiCommandResult result)
        {
            Sequence = sequence;
            CommandId = request != null ? request.CommandId : string.Empty;
            TraceId = request != null ? request.TraceId : string.Empty;
            Success = result != null && result.Success;
            ErrorCode = result != null ? result.ErrorCode : "unknown";
            Message = result != null ? result.Message : string.Empty;
        }

        public long Sequence { get; }
        public string CommandId { get; }
        public string TraceId { get; }
        public bool Success { get; }
        public string ErrorCode { get; }
        public string Message { get; }
    }

    public sealed class DebugUiCommandGate
    {
        private readonly List<DebugUiCommandLogEntry> _log = new List<DebugUiCommandLogEntry>();
        private long _nextSequence;
        private IDebugUiCommandProvider _provider;

        public DebugUiCommandGate(
            IDebugUiCommandProvider provider = null,
            DebugUiCommandGateOptions options = null)
        {
            _provider = provider ?? EmptyDebugUiCommandProvider.Instance;
            Options = options ?? new DebugUiCommandGateOptions();
        }

        public DebugUiCommandGateOptions Options { get; }
        public IDebugUiCommandProvider Provider => _provider;
        public IReadOnlyList<DebugUiCommandLogEntry> Log => _log;

        public void SetProvider(IDebugUiCommandProvider provider)
        {
            _provider = provider ?? EmptyDebugUiCommandProvider.Instance;
        }

        public DebugUiCommandResult Execute(DebugUiCommandRequest request)
        {
            request = request ?? new DebugUiCommandRequest(string.Empty);
            DebugUiCommandResult result;

            if (!Options.Enabled)
            {
                result = DebugUiCommandResult.Failed(request.CommandId, "disabled", "Debug UI command gate is disabled.", request.TraceId);
                AppendLog(request, result);
                return result;
            }

            try
            {
                if (!_provider.TryGetCommand(request.CommandId, out DebugUiCommandDescriptor descriptor))
                {
                    result = DebugUiCommandResult.Failed(request.CommandId, "not_found", "Debug UI command is not registered.", request.TraceId);
                    AppendLog(request, result);
                    return result;
                }

                if (descriptor.Risk == DebugUiCommandRisk.Destructive && !Options.AllowDestructiveCommands)
                {
                    result = DebugUiCommandResult.Failed(request.CommandId, "destructive_disabled", "Destructive Debug UI commands are disabled.", request.TraceId);
                    AppendLog(request, result);
                    return result;
                }

                if (descriptor.RequiresConfirmation && !request.Confirmed)
                {
                    result = DebugUiCommandResult.Failed(request.CommandId, "confirmation_required", "Debug UI command requires confirmation.", request.TraceId);
                    AppendLog(request, result);
                    return result;
                }

                result = _provider.Execute(request)
                    ?? DebugUiCommandResult.Failed(request.CommandId, "null_result", "Debug UI command returned no result.", request.TraceId);
            }
            catch (Exception exception)
            {
                result = DebugUiCommandResult.Failed(request.CommandId, exception.GetType().Name, exception.Message, request.TraceId);
            }

            AppendLog(request, result);
            return result;
        }

        private void AppendLog(DebugUiCommandRequest request, DebugUiCommandResult result)
        {
            _log.Add(new DebugUiCommandLogEntry(++_nextSequence, request, result));
            if (_log.Count > 32)
                _log.RemoveAt(0);
        }
    }

    public sealed class DebugUiCommandGateDebugSource : IFrameworkDebugSource
    {
        private readonly DebugUiCommandGate _gate;

        public DebugUiCommandGateDebugSource(DebugUiCommandGate gate, string name = "DebugCommands")
        {
            _gate = gate;
            Name = string.IsNullOrWhiteSpace(name) ? "DebugCommands" : name;
        }

        public string Name { get; }
        public FrameworkDebugMode Mode => FrameworkDebugMode.Runtime;
        public bool IsAvailable => _gate != null;

        public FrameworkDebugSnapshot CreateSnapshot()
        {
            if (_gate == null)
            {
                return new FrameworkDebugSnapshot(
                    Name,
                    Mode,
                    new[] { new FrameworkDebugSection("Status", "command gate unavailable") });
            }

            return new FrameworkDebugSnapshot(
                Name,
                Mode,
                new[]
                {
                    new FrameworkDebugSection("Commands", FormatCommands(_gate.Provider.Commands)),
                    new FrameworkDebugSection("Command Log", FormatLog(_gate.Log))
                });
        }

        private static string FormatCommands(IReadOnlyList<DebugUiCommandDescriptor> commands)
        {
            if (commands == null || commands.Count == 0)
                return "none";

            var builder = new StringBuilder();
            for (int i = 0; i < commands.Count; i++)
            {
                DebugUiCommandDescriptor command = commands[i];
                builder.Append(command.CommandId)
                    .Append(" risk=")
                    .Append(command.Risk)
                    .Append(" confirm=")
                    .Append(command.RequiresConfirmation ? "true" : "false");
                if (i + 1 < commands.Count)
                    builder.Append('\n');
            }

            return builder.ToString();
        }

        private static string FormatLog(IReadOnlyList<DebugUiCommandLogEntry> log)
        {
            if (log == null || log.Count == 0)
                return "none";

            var builder = new StringBuilder();
            for (int i = 0; i < log.Count; i++)
            {
                DebugUiCommandLogEntry entry = log[i];
                builder.Append('#')
                    .Append(entry.Sequence)
                    .Append(' ')
                    .Append(entry.CommandId)
                    .Append(" success=")
                    .Append(entry.Success ? "true" : "false");
                if (!string.IsNullOrEmpty(entry.ErrorCode))
                    builder.Append(" error=").Append(entry.ErrorCode);
                if (!string.IsNullOrEmpty(entry.Message))
                    builder.Append(" message=").Append(entry.Message);
                if (i + 1 < log.Count)
                    builder.Append('\n');
            }

            return builder.ToString();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Reflection;

namespace MxFramework.Config
{
    public interface IConfigReferenceProvider
    {
        void CollectReferences(ICollection<ConfigReference> references);
    }

    public readonly struct ConfigReference
    {
        public ConfigReference(Type ownerType, int ownerId, Type targetType, int targetId, string fieldName)
        {
            OwnerType = ownerType;
            OwnerId = ownerId;
            TargetType = targetType;
            TargetId = targetId;
            FieldName = fieldName ?? string.Empty;
        }

        public Type OwnerType { get; }
        public int OwnerId { get; }
        public Type TargetType { get; }
        public int TargetId { get; }
        public string FieldName { get; }
    }

    public readonly struct ConfigValidationIssue
    {
        public ConfigValidationIssue(ConfigValidationSeverity severity, ConfigError error, ConfigReference reference, string message)
        {
            Severity = severity;
            Error = error;
            Reference = reference;
            Message = message ?? string.Empty;
        }

        public ConfigValidationSeverity Severity { get; }
        public ConfigError Error { get; }
        public ConfigReference Reference { get; }
        public string Message { get; }
    }

    public enum ConfigValidationSeverity
    {
        Info,
        Warning,
        Error
    }

    public sealed class ConfigValidationReport
    {
        private readonly List<ConfigValidationIssue> _issues = new List<ConfigValidationIssue>();

        public IReadOnlyList<ConfigValidationIssue> Issues => _issues;
        public bool HasErrors { get; private set; }

        public void Add(ConfigValidationIssue issue)
        {
            _issues.Add(issue);
            if (issue.Severity == ConfigValidationSeverity.Error)
                HasErrors = true;
        }
    }

    public static class ConfigValidator
    {
        public static ConfigValidationReport ValidateReferences<T>(IConfigProvider provider) where T : IConfigData
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));

            var report = new ConfigValidationReport();
            var lookup = new ConfigReferenceLookup(provider);
            var references = new List<ConfigReference>();
            foreach (T config in provider.GetAllConfigs<T>())
            {
                if (config is IConfigReferenceProvider referenceProvider)
                    referenceProvider.CollectReferences(references);
            }

            for (int i = 0; i < references.Count; i++)
            {
                ConfigReference reference = references[i];
                if (reference.TargetId <= 0)
                {
                    report.Add(new ConfigValidationIssue(
                        ConfigValidationSeverity.Error,
                        ConfigError.InvalidId,
                        reference,
                        $"Invalid config reference id. Owner={reference.OwnerType.FullName}:{reference.OwnerId}, Field={reference.FieldName}, Target={reference.TargetType.FullName}:{reference.TargetId}."));
                    continue;
                }

                bool exists = lookup.Contains(reference.TargetType, reference.TargetId);
                if (!exists)
                {
                    report.Add(new ConfigValidationIssue(
                        ConfigValidationSeverity.Error,
                        ConfigError.NotFound,
                        reference,
                        $"Missing config reference. Owner={reference.OwnerType.FullName}:{reference.OwnerId}, Field={reference.FieldName}, Target={reference.TargetType.FullName}:{reference.TargetId}."));
                }
            }

            return report;
        }
    }

    // Bind each target type once per validation; never cache providers or query results across calls.
    internal sealed class ConfigReferenceLookup
    {
        private static readonly MethodInfo ContainsMethod = typeof(IConfigProvider).GetMethod(nameof(IConfigProvider.ContainsConfig));
        private readonly IConfigProvider _provider;
        private Dictionary<Type, Func<int, bool>> _queries;

        public ConfigReferenceLookup(IConfigProvider provider)
        {
            _provider = provider;
        }

        public bool Contains(Type type, int id)
        {
            if (_queries == null)
                _queries = new Dictionary<Type, Func<int, bool>>();

            // MakeGenericMethod retains the existing null/invalid target-type exception contract.
            if (type == null || !_queries.TryGetValue(type, out Func<int, bool> query))
            {
                MethodInfo method = ContainsMethod.MakeGenericMethod(type);
                query = (Func<int, bool>)method.CreateDelegate(typeof(Func<int, bool>), _provider);
                _queries.Add(type, query);
            }

            try
            {
                return query(id);
            }
            catch (Exception exception)
            {
                // Provider failures were wrapped by MethodInfo.Invoke in the original validators.
                throw new TargetInvocationException(exception);
            }
        }
    }
}

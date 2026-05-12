using System;
using System.Collections.Generic;

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

                bool exists = Contains(provider, reference.TargetType, reference.TargetId);
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

        private static bool Contains(IConfigProvider provider, Type type, int id)
        {
            foreach (object config in GetAll(provider, type))
            {
                if (config is IConfigData data && data.Id == id)
                    return true;
            }

            return false;
        }

        private static System.Collections.IEnumerable GetAll(IConfigProvider provider, Type type)
        {
            System.Reflection.MethodInfo method = typeof(IConfigProvider).GetMethod(nameof(IConfigProvider.GetAllConfigs));
            System.Reflection.MethodInfo generic = method.MakeGenericMethod(type);
            return (System.Collections.IEnumerable)generic.Invoke(provider, null);
        }
    }
}

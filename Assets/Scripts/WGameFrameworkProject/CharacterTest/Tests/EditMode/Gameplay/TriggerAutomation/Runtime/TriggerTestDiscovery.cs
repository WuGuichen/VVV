using System;
using System.Collections.Generic;
using System.Reflection;

namespace MxFramework.Tests.Gameplay.TriggerAutomation
{
    internal readonly struct TriggerTestDiscoveryError
    {
        internal TriggerTestDiscoveryError(string typeName, string message)
        {
            TypeName = typeName ?? string.Empty;
            Message = message ?? string.Empty;
        }

        internal string TypeName { get; }
        internal string Message { get; }
    }

    internal sealed class TriggerTestDiscoveryResult
    {
        private readonly ITriggerTestScenario[] _scenarios;
        private readonly TriggerTestDiscoveryError[] _errors;

        internal TriggerTestDiscoveryResult(
            ITriggerTestScenario[] scenarios,
            TriggerTestDiscoveryError[] errors)
        {
            _scenarios = scenarios ?? Array.Empty<ITriggerTestScenario>();
            _errors = errors ?? Array.Empty<TriggerTestDiscoveryError>();
        }

        internal IReadOnlyList<ITriggerTestScenario> Scenarios => _scenarios;
        internal IReadOnlyList<TriggerTestDiscoveryError> Errors => _errors;
        internal bool Succeeded => _errors.Length == 0;
    }

    internal static class TriggerTestScenarioDiscovery
    {
        internal static TriggerTestDiscoveryResult Discover(Assembly assembly)
        {
            if (assembly == null)
                throw new ArgumentNullException(nameof(assembly));

            return DiscoverTypes(assembly.GetTypes(), true);
        }

        internal static TriggerTestDiscoveryResult DiscoverTypes(
            IReadOnlyList<Type> types,
            bool requireAttribute)
        {
            var scenarios = new List<ITriggerTestScenario>();
            var errors = new List<TriggerTestDiscoveryError>();
            if (types == null)
                return new TriggerTestDiscoveryResult(
                    Array.Empty<ITriggerTestScenario>(),
                    Array.Empty<TriggerTestDiscoveryError>());

            for (int i = 0; i < types.Count; i++)
            {
                Type type = types[i];
                if (type == null ||
                    !typeof(ITriggerTestScenario).IsAssignableFrom(type))
                {
                    continue;
                }

                TriggerTestScenarioAttribute attribute =
                    type.GetCustomAttribute<TriggerTestScenarioAttribute>();
                if (requireAttribute && attribute == null)
                    continue;

                if (type.IsAbstract || type.IsInterface)
                {
                    errors.Add(Error(type, "Scenario type must be concrete."));
                    continue;
                }

                ConstructorInfo constructor = type.GetConstructor(
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic,
                    null,
                    Type.EmptyTypes,
                    null);
                if (constructor == null)
                {
                    errors.Add(Error(
                        type,
                        "Scenario type requires a parameterless constructor."));
                    continue;
                }

                try
                {
                    var scenario = (ITriggerTestScenario)
                        Activator.CreateInstance(type, true);
                    TriggerTestDescriptor descriptor = scenario.Descriptor;
                    if (descriptor == null)
                    {
                        errors.Add(Error(type, "Scenario descriptor is missing."));
                        continue;
                    }
                    if (!descriptor.ScenarioId.IsValid)
                    {
                        errors.Add(Error(
                            type,
                            "Scenario id is invalid: " +
                            descriptor.ScenarioId + "."));
                        continue;
                    }
                    if (attribute != null &&
                        !string.Equals(
                            attribute.ScenarioId,
                            descriptor.ScenarioId.Value,
                            StringComparison.Ordinal))
                    {
                        errors.Add(Error(
                            type,
                            "Scenario attribute id does not match the descriptor."));
                        continue;
                    }

                    scenarios.Add(scenario);
                }
                catch (Exception exception)
                {
                    errors.Add(Error(
                        type,
                        exception.GetType().Name + ": " + exception.Message));
                }
            }

            scenarios.Sort(CompareScenarios);
            for (int i = 1; i < scenarios.Count; i++)
            {
                if (scenarios[i - 1].Descriptor.ScenarioId.Equals(
                        scenarios[i].Descriptor.ScenarioId))
                {
                    errors.Add(new TriggerTestDiscoveryError(
                        scenarios[i].GetType().FullName,
                        "Duplicate scenario id: " +
                        scenarios[i].Descriptor.ScenarioId + "."));
                }
            }
            errors.Sort(CompareErrors);
            return new TriggerTestDiscoveryResult(
                scenarios.ToArray(),
                errors.ToArray());
        }

        private static TriggerTestDiscoveryError Error(
            Type type,
            string message) =>
            new TriggerTestDiscoveryError(type.FullName, message);

        private static int CompareScenarios(
            ITriggerTestScenario left,
            ITriggerTestScenario right) =>
            left.Descriptor.ScenarioId.CompareTo(right.Descriptor.ScenarioId);

        private static int CompareErrors(
            TriggerTestDiscoveryError left,
            TriggerTestDiscoveryError right)
        {
            int type = string.Compare(
                left.TypeName,
                right.TypeName,
                StringComparison.Ordinal);
            return type != 0
                ? type
                : string.Compare(
                    left.Message,
                    right.Message,
                    StringComparison.Ordinal);
        }
    }
}

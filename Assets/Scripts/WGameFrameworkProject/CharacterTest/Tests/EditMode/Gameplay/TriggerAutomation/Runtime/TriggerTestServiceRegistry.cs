using System;
using System.Collections.Generic;

namespace MxFramework.Tests.Gameplay.TriggerAutomation
{
    internal sealed class TriggerTestServiceRegistry
    {
        private readonly Dictionary<Type, object> _services =
            new Dictionary<Type, object>();

        internal void Register<T>(T service)
            where T : class
        {
            if (service == null)
                throw new ArgumentNullException(nameof(service));

            Type type = typeof(T);
            if (_services.ContainsKey(type))
                throw new InvalidOperationException(
                    "Trigger test service is already registered: " + type.FullName);

            _services.Add(type, service);
        }

        internal bool TryGet<T>(out T service)
            where T : class
        {
            if (_services.TryGetValue(typeof(T), out object value) &&
                value is T typed)
            {
                service = typed;
                return true;
            }

            service = null;
            return false;
        }
    }

    internal sealed class TriggerTestCounter
    {
        internal long Value { get; set; }
    }
}

using System;
using System.Collections.Generic;

namespace MxFramework.Runtime
{
    public interface IRuntimeServiceRegistry
    {
        bool TryGet<TService>(out TService service) where TService : class;
        TService Get<TService>() where TService : class;
    }

    public sealed class RuntimeServiceRegistry : IRuntimeServiceRegistry
    {
        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

        public void Register<TService>(TService service) where TService : class
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            _services[typeof(TService)] = service;
        }

        public bool TryGet<TService>(out TService service) where TService : class
        {
            object value;
            if (_services.TryGetValue(typeof(TService), out value))
            {
                service = (TService)value;
                return true;
            }

            service = null;
            return false;
        }

        public TService Get<TService>() where TService : class
        {
            TService service;
            if (TryGet(out service))
            {
                return service;
            }

            throw new InvalidOperationException("Runtime service is not registered: " + typeof(TService).FullName);
        }
    }

    public sealed class RuntimeHostContext
    {
        internal RuntimeHostContext(RuntimeHost host, RuntimeServiceRegistry services)
        {
            Host = host ?? throw new ArgumentNullException(nameof(host));
            Services = services ?? throw new ArgumentNullException(nameof(services));
        }

        public RuntimeHost Host { get; }
        public IRuntimeServiceRegistry Services { get; }
        public RuntimeLifecycleState LifecycleState => Host.State;
    }
}

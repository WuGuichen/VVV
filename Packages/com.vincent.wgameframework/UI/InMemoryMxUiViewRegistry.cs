using System;
using System.Collections.Generic;

namespace MxFramework.UI
{
    public sealed class InMemoryMxUiViewRegistry : IMxUiViewRegistry
    {
        private readonly Dictionary<MxUiViewId, Func<IMxUiView>> _factories = new Dictionary<MxUiViewId, Func<IMxUiView>>();

        public int Count => _factories.Count;

        public void Register(MxUiViewId id, Func<IMxUiView> factory)
        {
            if (!id.IsValid)
            {
                throw new ArgumentException("UI view id is required.", nameof(id));
            }

            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            if (_factories.ContainsKey(id))
            {
                throw new ArgumentException("UI view id is already registered: " + id + ".", nameof(id));
            }

            _factories.Add(id, factory);
        }

        public bool TryCreate(MxUiViewId id, out IMxUiView view)
        {
            view = null;
            Func<IMxUiView> factory;
            if (!_factories.TryGetValue(id, out factory))
            {
                return false;
            }

            view = factory();
            return true;
        }
    }
}

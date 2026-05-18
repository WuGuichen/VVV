using System;
using System.Collections.Generic;
using MxFramework.Diagnostics;

namespace MxFramework.DebugUI
{
    public sealed class FrameworkDebugSourceRegistry
    {
        private readonly List<IFrameworkDebugSource> _sources = new List<IFrameworkDebugSource>();
        private readonly HashSet<string> _names = new HashSet<string>(StringComparer.Ordinal);

        public IReadOnlyList<IFrameworkDebugSource> Sources => _sources;

        public bool Register(IFrameworkDebugSource source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            string name = source.Name;
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Debug source name cannot be empty.", nameof(source));

            if (!_names.Add(name))
                return false;

            _sources.Add(source);
            return true;
        }

        public bool Unregister(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            if (!_names.Remove(name))
                return false;

            for (int i = 0; i < _sources.Count; i++)
            {
                if (string.Equals(_sources[i].Name, name, StringComparison.Ordinal))
                {
                    _sources.RemoveAt(i);
                    return true;
                }
            }

            return true;
        }

        public bool TryGet(string name, out IFrameworkDebugSource source)
        {
            source = null;
            if (string.IsNullOrWhiteSpace(name))
                return false;

            for (int i = 0; i < _sources.Count; i++)
            {
                if (string.Equals(_sources[i].Name, name, StringComparison.Ordinal))
                {
                    source = _sources[i];
                    return true;
                }
            }

            return false;
        }

        public void Clear()
        {
            _sources.Clear();
            _names.Clear();
        }
    }
}

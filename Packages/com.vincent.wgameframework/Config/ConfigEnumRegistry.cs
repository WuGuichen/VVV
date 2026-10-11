using System;
using System.Collections.Generic;

namespace MxFramework.Config
{
    public sealed class ConfigEnumRegistry
    {
        private readonly Dictionary<string, ConfigEnumDomain> _domains = new Dictionary<string, ConfigEnumDomain>(StringComparer.Ordinal);

        public IReadOnlyCollection<ConfigEnumDomain> Domains => _domains.Values;

        public bool Register(ConfigEnumDomain domain, bool replace = false)
        {
            if (domain == null || !domain.IsValid)
                return false;

            if (_domains.ContainsKey(domain.EnumId) && !replace)
                return false;

            _domains[domain.EnumId] = domain;
            return true;
        }

        public bool TryGet(string enumId, out ConfigEnumDomain domain)
        {
            if (string.IsNullOrWhiteSpace(enumId))
            {
                domain = null;
                return false;
            }

            return _domains.TryGetValue(enumId, out domain);
        }
    }
}

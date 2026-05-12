using System.Collections.Generic;

namespace MxFramework.Config
{
    public interface IConfigTable<T> : IConfigProvider where T : IConfigData
    {
        ConfigSchema Schema { get; }
        IReadOnlyCollection<T> Rows { get; }
        void Add(T row);
        ConfigTableValidationReport Validate(IConfigProvider resolver = null, ILocalizationProvider localizationProvider = null);
    }
}

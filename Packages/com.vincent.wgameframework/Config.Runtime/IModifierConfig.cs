using MxFramework.Config;

namespace MxFramework.Config.Runtime
{
    public interface IModifierConfig : IConfigData
    {
        LocalizedTextKey NameText { get; }
        LocalizedTextKey DescriptionText { get; }
        int ParamIndex { get; }
        int[] Parameters { get; }
    }
}

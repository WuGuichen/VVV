using MxFramework.Config;

namespace MxFramework.Config.Runtime
{
    public interface IBuffConfig : IConfigData
    {
        LocalizedTextKey NameText { get; }
        LocalizedTextKey DescriptionText { get; }
        float Duration { get; }
        int MaxLayers { get; }
        bool IsPermanent { get; }
        int ModifierId { get; }
    }
}

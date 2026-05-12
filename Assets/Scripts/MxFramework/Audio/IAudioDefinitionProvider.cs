namespace MxFramework.Audio
{
    public interface IAudioDefinitionProvider
    {
        bool TryGetEvent(int eventId, out AudioEventDefinition definition);
        bool TryGetBus(int busId, out AudioBusDefinition definition);
        bool TryGetParameter(int eventId, int parameterId, out AudioParameterDefinition definition);
    }
}

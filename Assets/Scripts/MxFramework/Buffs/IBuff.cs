namespace MxFramework.Buffs
{
    public interface IBuff
    {
        int Id { get; }
        float Duration { get; }
        float RemainingTime { get; }
        int MaxLayers { get; }
        int CurrentLayers { get; }
        bool IsPermanent { get; }
        bool IsExpired { get; }

        void OnAttach(IBuffTarget target);
        void OnTick(float deltaTime, IBuffTarget target);
        void OnDetach(IBuffTarget target);
        int AddLayer(int count);
        void RefreshDuration();
    }
}

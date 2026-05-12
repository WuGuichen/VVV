using System;

namespace MxFramework.Buffs
{
    /// <summary>
    /// Generic buff lifecycle extracted from WGame BuffData/BuffStatus timing and layer rules.
    /// </summary>
    public abstract class BuffBase : IBuff
    {
        protected BuffBase(int id, float duration, int maxLayers = 1, bool isPermanent = false)
        {
            if (maxLayers <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxLayers));

            Id = id;
            Duration = duration;
            RemainingTime = isPermanent ? float.PositiveInfinity : Math.Max(0f, duration);
            MaxLayers = maxLayers;
            CurrentLayers = 1;
            IsPermanent = isPermanent;
        }

        public int Id { get; }
        public float Duration { get; }
        public float RemainingTime { get; private set; }
        public int MaxLayers { get; }
        public int CurrentLayers { get; private set; }
        public bool IsPermanent { get; }
        public bool IsExpired => !IsPermanent && RemainingTime <= 0f;

        public virtual void OnAttach(IBuffTarget target)
        {
        }

        public virtual void OnTick(float deltaTime, IBuffTarget target)
        {
            if (IsPermanent || deltaTime <= 0f)
                return;

            RemainingTime = Math.Max(0f, RemainingTime - deltaTime);
        }

        public virtual void OnDetach(IBuffTarget target)
        {
        }

        public int AddLayer(int count)
        {
            if (count <= 0)
                return 0;

            int oldLayer = CurrentLayers;
            CurrentLayers = Math.Min(MaxLayers, CurrentLayers + count);
            return count - (CurrentLayers - oldLayer);
        }

        public void RefreshDuration()
        {
            if (!IsPermanent)
                RemainingTime = Math.Max(0f, Duration);
        }
    }
}

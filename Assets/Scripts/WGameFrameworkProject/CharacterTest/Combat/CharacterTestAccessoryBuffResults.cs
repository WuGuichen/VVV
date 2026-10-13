using System;
using MxFramework.Gameplay;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Pre-sized terminal buffer; lineage is committed by the owner after actual Buff application.</summary>
    internal sealed class CharacterTestAccessoryBuffResults : IGameplayBuffApplicationResultSink
    {
        private GameplayBuffApplicationResult[] _results = Array.Empty<GameplayBuffApplicationResult>();
        internal int Count { get; private set; }
        internal bool Overflowed { get; private set; }
        internal GameplayBuffApplicationResult At(int index) => _results[index];
        internal void Prepare(int count)
        {
            if (_results.Length < count) _results = new GameplayBuffApplicationResult[Math.Max(16, count)];
            Count = 0; Overflowed = false;
        }
        public void Publish(in GameplayBuffApplicationResult result)
        {
            if (Count == _results.Length) { Overflowed = true; return; }
            _results[Count++] = result;
        }
    }
}

using System;
using MagicaCloth2;
using UnityEngine;

namespace MxFramework.CharacterCloth.Magica.Unity
{
    /// <summary>Exclusive main-thread lease for Magica's GLOBAL frequency and maximum steps per frame.</summary>
    /// <remarks>LowFreqAlloc on acquisition. This changes all Magica teams, never one character.
    /// Dispose with the owning session. Other MagicaSettings/global writers must not compete with this lease.</remarks>
    public sealed class MagicaClothSimulationBudget : IDisposable
    {
        private static MagicaClothSimulationBudget s_current;
        private static int s_previewScopes;
        private static int s_generation;
        private readonly int _previousFrequency;
        private readonly int _previousSteps;
        private bool _disposed;
        /// <summary>Gets the requested global frequency in Hz (30..150).</summary>
        public int Frequency { get; private set; }
        /// <summary>Gets the requested global maximum simulation steps per rendered frame (1..5).</summary>
        public int MaximumStepsPerFrame { get; private set; }

        private MagicaClothSimulationBudget(int frequency, int steps)
        {
            _previousFrequency = MagicaManager.GetSimulationFrequency();
            _previousSteps = MagicaManager.GetMaxSimulationCountPerFrame();
            Frequency = frequency;
            MaximumStepsPerFrame = steps;
        }
        /// <summary>Acquires the single global budget after Magica initializes; false means busy, previewing or not ready.</summary>
        /// <remarks>LowFreqAlloc only on successful acquisition. No resource defaults are changed.</remarks>
        public static bool TryAcquire(int frequency, int maximumStepsPerFrame, out MagicaClothSimulationBudget budget)
        {
            Validate(frequency, maximumStepsPerFrame);
            budget = null;
            if (s_current != null || s_previewScopes > 0 || !MagicaManager.IsPlaying()) return false;
            budget = new MagicaClothSimulationBudget(frequency, maximumStepsPerFrame);
            s_current = budget;
            budget.Apply();
            return true;
        }
        /// <summary>Updates this lease; during an independent preview the request is deferred until it closes.</summary>
        /// <remarks>NoAlloc. Returns false if the manager is unavailable; retry after initialization.</remarks>
        public bool SetBudget(int frequency, int maximumStepsPerFrame)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(MagicaClothSimulationBudget));
            Validate(frequency, maximumStepsPerFrame);
            Frequency = frequency;
            MaximumStepsPerFrame = maximumStepsPerFrame;
            if (!MagicaManager.IsPlaying()) return false;
            if (s_previewScopes == 0) Apply();
            return true;
        }
        /// <summary>Temporarily restores the pre-gameplay budget for an independent cloth preview.</summary>
        /// <remarks>LowFreqAlloc. Nestable and idempotent on disposal; the backend remains inherently global.</remarks>
        public static IDisposable SuspendForIndependentPreview()
        {
            if (s_previewScopes++ == 0) s_current?.Restore();
            return new PreviewScope();
        }
        /// <summary>Restores the original budget and releases global ownership.</summary>
        /// <remarks>NoAlloc. Repeated calls are harmless.</remarks>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (!ReferenceEquals(s_current, this)) return;
            Restore();
            s_current = null;
        }
        private void Apply()
        {
            MagicaManager.SetSimulationFrequency(Frequency);
            MagicaManager.SetMaxSimulationCountPerFrame(MaximumStepsPerFrame);
        }
        private void Restore()
        {
            if (!MagicaManager.IsPlaying()) return;
            MagicaManager.SetSimulationFrequency(_previousFrequency);
            MagicaManager.SetMaxSimulationCountPerFrame(_previousSteps);
        }
        private static void Validate(int frequency, int steps)
        {
            if (frequency < 30 || frequency > 150) throw new ArgumentOutOfRangeException(nameof(frequency));
            if (steps < 1 || steps > 5) throw new ArgumentOutOfRangeException(nameof(steps));
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            if (s_current != null) s_current._disposed = true;
            s_current = null;
            s_previewScopes = 0;
            s_generation++;
        }
        private sealed class PreviewScope : IDisposable
        {
            private bool _disposed;
            private readonly int _generation = s_generation;
            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                if (_generation != s_generation) return;
                if (s_previewScopes > 0 && --s_previewScopes == 0 && MagicaManager.IsPlaying())
                    s_current?.Apply();
            }
        }
    }
}

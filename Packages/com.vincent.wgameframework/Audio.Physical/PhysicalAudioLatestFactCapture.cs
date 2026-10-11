using System;
using System.Threading;

namespace MxFramework.Audio.Physical
{
    /// <summary>Immutable view of the latest physical-audio playback that reached a runtime intent queue.</summary>
    /// <remarks>NoAlloc. This diagnostic snapshot is not runtime authority and must not affect gameplay decisions.</remarks>
    public readonly struct PhysicalAudioLatestFactSnapshot
    {
        internal PhysicalAudioLatestFactSnapshot(
            in PhysicalAudioInteractionFact fact,
            in PhysicalAudioResolvedPlayback result,
            in PhysicalAudioContactDynamics dynamics,
            bool hasDynamics,
            int parameterCount,
            int revision)
        {
            Fact = fact;
            Result = result;
            Dynamics = dynamics;
            HasDynamics = hasDynamics;
            ParameterCount = parameterCount;
            Revision = revision;
        }

        public PhysicalAudioInteractionFact Fact { get; }

        public PhysicalAudioResolvedPlayback Result { get; }

        public PhysicalAudioContactDynamics Dynamics { get; }

        public bool HasDynamics { get; }

        public int ParameterCount { get; }

        public int Revision { get; }
    }

    /// <summary>Fixed-capacity diagnostic sink used by Editor tooling to read the latest real runtime interaction.</summary>
    /// <remarks>
    /// NoAlloc after static initialization. Runtime writers publish only after a playback intent is accepted.
    /// This is a single-slot diagnostic seam; consumers must not use it as gameplay or replay authority.
    /// </remarks>
    public static class PhysicalAudioLatestFactCapture
    {
        public const int ParameterCapacity = 32;

        private static readonly AudioParameterValue[] ParameterValues = new AudioParameterValue[ParameterCapacity];
        private static PhysicalAudioInteractionFact s_fact;
        private static PhysicalAudioResolvedPlayback s_result;
        private static PhysicalAudioContactDynamics s_dynamics;
        private static bool s_hasDynamics;
        private static int s_parameterCount;
        private static int s_hasValue;
        private static int s_sequence;

        /// <summary>Publishes one real resolved playback into the fixed diagnostic slot.</summary>
        /// <remarks>NoAlloc after static initialization. Safe for multiple writers and concurrent diagnostic readers.</remarks>
        public static bool Publish(
            in PhysicalAudioInteractionFact fact,
            in PhysicalAudioResolvedPlayback result,
            ReadOnlySpan<AudioParameterValue> parameters)
        {
            return Publish(fact, result, parameters, default, false);
        }

        /// <summary>Publishes one real resolved playback and its raw contact dynamics into the diagnostic slot.</summary>
        /// <remarks>NoAlloc after static initialization. Raw dynamics remain diagnostic-only.</remarks>
        public static bool Publish(
            in PhysicalAudioInteractionFact fact,
            in PhysicalAudioResolvedPlayback result,
            ReadOnlySpan<AudioParameterValue> parameters,
            in PhysicalAudioContactDynamics dynamics)
        {
            return Publish(fact, result, parameters, dynamics, dynamics.IsResolved);
        }

        private static bool Publish(
            in PhysicalAudioInteractionFact fact,
            in PhysicalAudioResolvedPlayback result,
            ReadOnlySpan<AudioParameterValue> parameters,
            in PhysicalAudioContactDynamics dynamics,
            bool hasDynamics)
        {
            int parameterCount = result.ParameterCount;
            if (!result.Success
                || parameterCount < 0
                || parameterCount > ParameterCapacity
                || parameters.Length < parameterCount)
                return false;

            int observed;
            while (true)
            {
                observed = Volatile.Read(ref s_sequence);
                if ((observed & 1) != 0)
                {
                    Thread.Yield();
                    continue;
                }
                if (Interlocked.CompareExchange(ref s_sequence, observed + 1, observed) == observed)
                    break;
            }

            s_fact = fact;
            s_result = result;
            s_dynamics = dynamics;
            s_hasDynamics = hasDynamics;
            for (int i = 0; i < parameterCount; i++)
                ParameterValues[i] = parameters[i];
            s_parameterCount = parameterCount;
            Volatile.Write(ref s_hasValue, 1);
            Volatile.Write(ref s_sequence, observed + 2);
            return true;
        }

        /// <summary>Copies the latest real interaction and resolved parameters into caller-owned storage.</summary>
        /// <remarks>NoAlloc. Returns false when no real playback has been published or the buffer is too small.</remarks>
        public static bool TryRead(
            Span<AudioParameterValue> parameterBuffer,
            out PhysicalAudioLatestFactSnapshot snapshot)
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                int begin = Volatile.Read(ref s_sequence);
                if ((begin & 1) != 0)
                    continue;
                if (Volatile.Read(ref s_hasValue) == 0)
                {
                    snapshot = default;
                    return false;
                }

                PhysicalAudioInteractionFact fact = s_fact;
                PhysicalAudioResolvedPlayback result = s_result;
                PhysicalAudioContactDynamics dynamics = s_dynamics;
                bool hasDynamics = s_hasDynamics;
                int parameterCount = s_parameterCount;
                if (parameterCount < 0
                    || parameterCount > ParameterCapacity
                    || parameterBuffer.Length < parameterCount)
                {
                    snapshot = default;
                    return false;
                }
                for (int i = 0; i < parameterCount; i++)
                    parameterBuffer[i] = ParameterValues[i];

                int end = Volatile.Read(ref s_sequence);
                if (begin == end && (end & 1) == 0)
                {
                    snapshot = new PhysicalAudioLatestFactSnapshot(
                        fact,
                        result,
                        dynamics,
                        hasDynamics,
                        parameterCount,
                        end >> 1);
                    return true;
                }
            }

            snapshot = default;
            return false;
        }
    }
}

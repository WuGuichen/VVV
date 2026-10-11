using System;
using System.Collections.Generic;

namespace MxFramework.Audio.Physical
{
    public readonly struct PhysicalAudioResolveTraceEntry
    {
        public PhysicalAudioResolveTraceEntry(
            PhysicalAudioFamilyId familyId,
            AudioBasisId basisId,
            int fallbackDepth,
            bool matched,
            PhysicalAudioBasisRejectionReason rejectionReason,
            float matchDistance)
            : this(
                familyId,
                AcousticMaterialPairId.None,
                PhysicalAudioVariantKind.Unknown,
                basisId,
                fallbackDepth,
                0,
                0,
                matched,
                rejectionReason,
                matchDistance)
        {
        }

        public PhysicalAudioResolveTraceEntry(
            PhysicalAudioFamilyId familyId,
            AcousticMaterialPairId materialPairId,
            PhysicalAudioVariantKind variantKind,
            AudioBasisId basisId,
            int fallbackDepth,
            int materialAFallbackDepth,
            int materialBFallbackDepth,
            bool matched,
            PhysicalAudioBasisRejectionReason rejectionReason,
            float matchDistance)
        {
            FamilyId = familyId;
            MaterialPairId = materialPairId;
            VariantKind = variantKind;
            BasisId = basisId;
            FallbackDepth = fallbackDepth;
            MaterialAFallbackDepth = materialAFallbackDepth;
            MaterialBFallbackDepth = materialBFallbackDepth;
            Matched = matched;
            RejectionReason = rejectionReason;
            MatchDistance = matchDistance;
        }

        public PhysicalAudioFamilyId FamilyId { get; }
        public AcousticMaterialPairId MaterialPairId { get; }
        public PhysicalAudioVariantKind VariantKind { get; }
        public AudioBasisId BasisId { get; }
        public int FallbackDepth { get; }
        public int MaterialAFallbackDepth { get; }
        public int MaterialBFallbackDepth { get; }
        public bool Matched { get; }
        public PhysicalAudioBasisRejectionReason RejectionReason { get; }
        public float MatchDistance { get; }
    }

    /// <summary>Allocated diagnostic explanation for a resolver invocation.</summary>
    /// <remarks>AllocByDesign. Do not call from Tick paths.</remarks>
    public sealed class PhysicalAudioResolveTraceSnapshot
    {
        internal PhysicalAudioResolveTraceSnapshot(
            in PhysicalAudioInteractionFact fact,
            in PhysicalAudioResolvedPlayback result,
            AudioParameterValue[] parameters,
            PhysicalAudioResolveTraceEntry[] entries,
            AcousticMaterialId[] impactorMaterialFallbackPath,
            AcousticMaterialId[] receiverMaterialFallbackPath)
        {
            Fact = fact;
            Result = result;
            Parameters = parameters ?? Array.Empty<AudioParameterValue>();
            Entries = entries ?? Array.Empty<PhysicalAudioResolveTraceEntry>();
            ImpactorMaterialFallbackPath = impactorMaterialFallbackPath ?? Array.Empty<AcousticMaterialId>();
            ReceiverMaterialFallbackPath = receiverMaterialFallbackPath ?? Array.Empty<AcousticMaterialId>();
        }

        public PhysicalAudioInteractionFact Fact { get; }
        public PhysicalAudioResolvedPlayback Result { get; }
        public AudioParameterValue[] Parameters { get; }
        public PhysicalAudioResolveTraceEntry[] Entries { get; }

        /// <summary>Directed impactor material path from the original fact to the selected Pair side.</summary>
        public AcousticMaterialId[] ImpactorMaterialFallbackPath { get; }

        /// <summary>Directed receiver material path from the original fact to the selected Pair side.</summary>
        public AcousticMaterialId[] ReceiverMaterialFallbackPath { get; }
    }

    public sealed partial class PhysicalAudioResolver
    {
        /// <summary>Captures candidate decisions and final parameter values for authoring/debug use.</summary>
        /// <remarks>AllocByDesign. Do not call from Tick paths.</remarks>
        public PhysicalAudioResolveTraceSnapshot CaptureTrace(in PhysicalAudioInteractionFact fact)
        {
            var trace = new TraceBuilder();
            var parameters = new AudioParameterValue[_definitions.MaxResponseParameterCount];
            ResolveCore(fact, parameters.AsSpan(), trace, out PhysicalAudioResolvedPlayback result);
            if (result.ParameterCount != parameters.Length)
                Array.Resize(ref parameters, result.Success ? result.ParameterCount : 0);
            int impactorFallbackDepth = 0;
            int receiverFallbackDepth = 0;
            if (result.Success && result.MaterialPairId.IsValid)
            {
                for (int i = 0; i < trace.Entries.Count; i++)
                {
                    PhysicalAudioResolveTraceEntry entry = trace.Entries[i];
                    if (!entry.Matched || entry.BasisId != result.BasisId || entry.MaterialPairId != result.MaterialPairId)
                        continue;
                    impactorFallbackDepth = entry.MaterialAFallbackDepth;
                    receiverFallbackDepth = entry.MaterialBFallbackDepth;
                    break;
                }
            }
            return new PhysicalAudioResolveTraceSnapshot(
                fact,
                result,
                parameters,
                trace.Entries.ToArray(),
                BuildMaterialFallbackPath(fact.Impactor.MaterialId, impactorFallbackDepth),
                BuildMaterialFallbackPath(fact.Receiver.MaterialId, receiverFallbackDepth));
        }

        private AcousticMaterialId[] BuildMaterialFallbackPath(AcousticMaterialId original, int depth)
        {
            if (!original.IsValid)
                return Array.Empty<AcousticMaterialId>();
            var path = new AcousticMaterialId[Math.Max(0, depth) + 1];
            path[0] = original;
            AcousticMaterialId current = original;
            for (int i = 1; i < path.Length; i++)
            {
                if (!_definitions.TryGetMaterial(current, out AcousticMaterialProfileDefinition material)
                    || !material.FallbackMaterialId.IsValid)
                {
                    Array.Resize(ref path, i);
                    break;
                }
                current = material.FallbackMaterialId;
                path[i] = current;
            }
            return path;
        }

        private sealed class TraceBuilder
        {
            public readonly List<PhysicalAudioResolveTraceEntry> Entries = new List<PhysicalAudioResolveTraceEntry>();

            public void Add(in PhysicalAudioResolveTraceEntry entry) => Entries.Add(entry);
        }
    }
}

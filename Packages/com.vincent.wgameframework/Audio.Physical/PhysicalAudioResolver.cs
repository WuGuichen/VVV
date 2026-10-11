using System;
using System.Collections.Generic;

namespace MxFramework.Audio.Physical
{
    public enum PhysicalAudioResolveFailureReason
    {
        None = 0,
        InvalidInteraction = 1,
        MissingFamily = 2,
        MissingBasis = 3,
        BufferTooSmall = 4,
        FallbackCycle = 5,
        MissingResponseProfile = 6,
        MissingMaterialPair = 7
    }

    public enum PhysicalAudioBasisRejectionReason
    {
        None = 0,
        ImpactorMaterial = 1,
        ReceiverMaterial = 2,
        ImpactorMass = 3,
        ReceiverMass = 4,
        ImpactorContactArea = 5,
        ReceiverContactArea = 6,
        ImpactorSurface = 7,
        ReceiverSurface = 8,
        ImpactorConstruction = 9,
        ReceiverConstruction = 10,
        RelativeSpeed = 11,
        Intensity = 12,
        Angle = 13
    }

    /// <summary>Result of one deterministic Basis Sound selection.</summary>
    /// <remarks>NoAlloc.</remarks>
    public readonly struct PhysicalAudioResolvedPlayback
    {
        internal PhysicalAudioResolvedPlayback(
            bool success,
            PhysicalAudioResolveFailureReason failureReason,
            PhysicalAudioFamilyId familyId,
            AudioBasisId basisId,
            int cueId,
            int priority,
            int cooldownFrames,
            float matchDistance,
            int parameterCount,
            bool usedFallback)
            : this(
                success,
                failureReason,
                familyId,
                AcousticMaterialPairId.None,
                PhysicalAudioVariantKind.Unknown,
                basisId,
                cueId,
                priority,
                cooldownFrames,
                matchDistance,
                parameterCount,
                usedFallback)
        {
        }

        internal PhysicalAudioResolvedPlayback(
            bool success,
            PhysicalAudioResolveFailureReason failureReason,
            PhysicalAudioFamilyId familyId,
            AcousticMaterialPairId materialPairId,
            PhysicalAudioVariantKind variantKind,
            AudioBasisId basisId,
            int cueId,
            int priority,
            int cooldownFrames,
            float matchDistance,
            int parameterCount,
            bool usedFallback)
        {
            Success = success;
            FailureReason = failureReason;
            FamilyId = familyId;
            MaterialPairId = materialPairId;
            VariantKind = variantKind;
            BasisId = basisId;
            CueId = cueId;
            Priority = priority;
            CooldownFrames = cooldownFrames;
            MatchDistance = matchDistance;
            ParameterCount = parameterCount;
            UsedFallback = usedFallback;
        }

        public bool Success { get; }
        public PhysicalAudioResolveFailureReason FailureReason { get; }
        public PhysicalAudioFamilyId FamilyId { get; }
        public AcousticMaterialPairId MaterialPairId { get; }
        public PhysicalAudioVariantKind VariantKind { get; }
        public AudioBasisId BasisId { get; }
        public int CueId { get; }
        public int Priority { get; }
        public int CooldownFrames { get; }
        public float MatchDistance { get; }
        public int ParameterCount { get; }
        public bool UsedFallback { get; }

        internal static PhysicalAudioResolvedPlayback Failed(
            PhysicalAudioResolveFailureReason reason,
            int requiredParameterCount = 0)
        {
            return new PhysicalAudioResolvedPlayback(
                false,
                reason,
                PhysicalAudioFamilyId.None,
                AcousticMaterialPairId.None,
                PhysicalAudioVariantKind.Unknown,
                AudioBasisId.None,
                0,
                0,
                0,
                0f,
                requiredParameterCount,
                false);
        }
    }

    /// <summary>One immutable candidate decision in an allocated resolver trace.</summary>
    /// <remarks>NoAlloc.</remarks>
    /// <summary>Deterministically selects one complete Basis Sound and evaluates its response curves.</summary>
    /// <remarks>
    /// LowFreqAlloc when instantiated. <see cref="TryResolve"/> is NoAlloc after provider initialization;
    /// <see cref="CaptureTrace"/> is AllocByDesign and must not be called from Tick paths.
    /// </remarks>
    public sealed partial class PhysicalAudioResolver
    {
        private readonly IPhysicalAudioDefinitionProvider _definitions;

        /// <summary>Creates a resolver over one immutable startup-built definition provider.</summary>
        /// <remarks>LowFreqAlloc. Construct during composition, outside Tick paths.</remarks>
        public PhysicalAudioResolver(IPhysicalAudioDefinitionProvider definitions)
        {
            _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
        }

        /// <summary>Selects one Basis Sound and writes parameter values into caller-owned storage.</summary>
        /// <remarks>NoAlloc after the definition provider has been initialized.</remarks>
        public bool TryResolve(
            in PhysicalAudioInteractionFact fact,
            Span<AudioParameterValue> parameterBuffer,
            out PhysicalAudioResolvedPlayback result)
        {
            return ResolveCore(fact, parameterBuffer, null, out result);
        }

        private bool ResolveCore(
            in PhysicalAudioInteractionFact fact,
            Span<AudioParameterValue> parameterBuffer,
            TraceBuilder trace,
            out PhysicalAudioResolvedPlayback result)
        {
            if (fact.Kind == PhysicalAudioInteractionKind.Unknown || fact.Phase != PhysicalAudioInteractionPhase.Started)
            {
                result = PhysicalAudioResolvedPlayback.Failed(PhysicalAudioResolveFailureReason.InvalidInteraction);
                return false;
            }

            int familyCount = _definitions.GetFamilyCandidateCount(fact.Kind, fact.Mode);
            PhysicalAudioInteractionMode queryMode = fact.Mode;
            if (familyCount == 0 && fact.Mode != PhysicalAudioInteractionMode.Any)
            {
                queryMode = PhysicalAudioInteractionMode.Any;
                familyCount = _definitions.GetFamilyCandidateCount(fact.Kind, queryMode);
            }
            if (familyCount == 0)
            {
                result = PhysicalAudioResolvedPlayback.Failed(PhysicalAudioResolveFailureReason.MissingFamily);
                return false;
            }

            bool found = false;
            ResolvedCandidate best = default;
            bool fallbackCycle = false;
            for (int i = 0; i < familyCount; i++)
            {
                if (!_definitions.TryGetFamilyCandidate(fact.Kind, queryMode, i, out PhysicalAudioFamilyDefinition family))
                    continue;
                if (TryResolveFamilyChain(fact, family, trace, out ResolvedCandidate candidate, out bool chainCycle)
                    && (!found || IsBetter(candidate, best)))
                {
                    best = candidate;
                    found = true;
                }
                fallbackCycle |= chainCycle;
            }

            if (!found)
            {
                result = PhysicalAudioResolvedPlayback.Failed(
                    fallbackCycle ? PhysicalAudioResolveFailureReason.FallbackCycle : PhysicalAudioResolveFailureReason.MissingBasis);
                return false;
            }

            int parameterCount = 0;
            if (best.Family.ResponseProfileId.IsValid)
            {
                if (!_definitions.TryGetResponseProfile(best.Family.ResponseProfileId, out PhysicalAudioResponseProfileDefinition profile))
                {
                    result = PhysicalAudioResolvedPlayback.Failed(PhysicalAudioResolveFailureReason.MissingResponseProfile);
                    return false;
                }
                parameterCount = profile.CurveCount;
                if (parameterBuffer.Length < parameterCount)
                {
                    result = PhysicalAudioResolvedPlayback.Failed(PhysicalAudioResolveFailureReason.BufferTooSmall, parameterCount);
                    return false;
                }
                EvaluateResponse(fact, profile, parameterBuffer);
            }

            result = new PhysicalAudioResolvedPlayback(
                true,
                PhysicalAudioResolveFailureReason.None,
                best.Family.Id,
                best.Basis.MaterialPairId,
                best.Basis.VariantKind,
                best.Basis.Id,
                best.Basis.CueId,
                best.Basis.Priority,
                best.Family.CooldownFrames,
                best.MatchDistance,
                parameterCount,
                best.FallbackDepth > 0 || best.ForcedFallback);
            return true;
        }

        private bool TryResolveFamilyChain(
            in PhysicalAudioInteractionFact fact,
            in PhysicalAudioFamilyDefinition initialFamily,
            TraceBuilder trace,
            out ResolvedCandidate candidate,
            out bool cycle)
        {
            PhysicalAudioFamilyDefinition family = initialFamily;
            int fallbackDepth = 0;
            while (fallbackDepth <= _definitions.FamilyCount)
            {
                if (TryResolveBasis(fact, family, fallbackDepth, trace, out candidate))
                {
                    cycle = false;
                    return true;
                }
                if (family.FallbackBasisId.IsValid
                    && _definitions.TryGetBasis(family.FallbackBasisId, out PhysicalAudioBasisDefinition fallbackBasis))
                {
                    candidate = new ResolvedCandidate(family, fallbackBasis, fallbackDepth, float.MaxValue, true);
                    trace?.Add(new PhysicalAudioResolveTraceEntry(
                        family.Id,
                        fallbackBasis.Id,
                        fallbackDepth,
                        true,
                        PhysicalAudioBasisRejectionReason.None,
                        float.MaxValue));
                    cycle = false;
                    return true;
                }
                if (!family.FallbackFamilyId.IsValid
                    || !_definitions.TryGetFamily(family.FallbackFamilyId, out family))
                {
                    candidate = default;
                    cycle = false;
                    return false;
                }
                fallbackDepth++;
            }
            candidate = default;
            cycle = true;
            return false;
        }

        private bool TryResolveBasis(
            in PhysicalAudioInteractionFact fact,
            in PhysicalAudioFamilyDefinition family,
            int fallbackDepth,
            TraceBuilder trace,
            out ResolvedCandidate result)
        {
            if (_definitions.MaterialPairCount > 0 && family.VariantKind != PhysicalAudioVariantKind.Unknown)
                return TryResolvePairBasis(fact, family, fallbackDepth, trace, out result);

            bool found = false;
            PhysicalAudioBasisDefinition bestBasis = default;
            float bestDistance = float.MaxValue;
            int count = _definitions.GetBasisCandidateCount(family.Id);
            for (int i = 0; i < count; i++)
            {
                if (!_definitions.TryGetBasisCandidate(family.Id, i, out PhysicalAudioBasisDefinition basis))
                    continue;
                if (!TryScore(fact, family.Weights, basis, out float distance, out PhysicalAudioBasisRejectionReason rejection))
                {
                    trace?.Add(new PhysicalAudioResolveTraceEntry(family.Id, basis.Id, fallbackDepth, false, rejection, 0f));
                    continue;
                }
                trace?.Add(new PhysicalAudioResolveTraceEntry(family.Id, basis.Id, fallbackDepth, true, PhysicalAudioBasisRejectionReason.None, distance));
                if (!found
                    || basis.Priority > bestBasis.Priority
                    || (basis.Priority == bestBasis.Priority && distance < bestDistance)
                    || (basis.Priority == bestBasis.Priority && NearlyEqual(distance, bestDistance) && basis.Id.Value < bestBasis.Id.Value))
                {
                    found = true;
                    bestBasis = basis;
                    bestDistance = distance;
                }
            }
            result = found
                ? new ResolvedCandidate(family, bestBasis, fallbackDepth, bestDistance, false)
                : default;
            return found;
        }

        private bool TryResolvePairBasis(
            in PhysicalAudioInteractionFact fact,
            in PhysicalAudioFamilyDefinition family,
            int familyFallbackDepth,
            TraceBuilder trace,
            out ResolvedCandidate result)
        {
            bool pairFound = false;
            AcousticMaterialPairDefinition bestPair = default;
            int bestFirstDepth = int.MaxValue;
            int bestSecondDepth = int.MaxValue;
            bool bestFirstMapsToA = true;

            AcousticMaterialId first = fact.Impactor.MaterialId;
            for (int firstDepth = 0; first.IsValid && firstDepth <= _definitions.MaterialCount; firstDepth++)
            {
                AcousticMaterialId second = fact.Receiver.MaterialId;
                for (int secondDepth = 0; second.IsValid && secondDepth <= _definitions.MaterialCount; secondDepth++)
                {
                    if (_definitions.TryFindMaterialPair(first, second, out AcousticMaterialPairDefinition pair)
                        && _definitions.GetPairVariantBasisCandidateCount(pair.Id, family.VariantKind) > 0
                        && (!pairFound || IsBetterPairFallback(
                            firstDepth,
                            secondDepth,
                            pair.Id.Value,
                            bestFirstDepth,
                            bestSecondDepth,
                            bestPair.Id.Value)))
                    {
                        pairFound = true;
                        bestPair = pair;
                        bestFirstDepth = firstDepth;
                        bestSecondDepth = secondDepth;
                        bestFirstMapsToA = first.Value <= second.Value;
                    }

                    if (!_definitions.TryGetMaterial(second, out AcousticMaterialProfileDefinition secondMaterial)
                        || !secondMaterial.FallbackMaterialId.IsValid)
                        break;
                    second = secondMaterial.FallbackMaterialId;
                }

                if (!_definitions.TryGetMaterial(first, out AcousticMaterialProfileDefinition firstMaterial)
                    || !firstMaterial.FallbackMaterialId.IsValid)
                    break;
                first = firstMaterial.FallbackMaterialId;
            }

            if (!pairFound)
            {
                result = default;
                return false;
            }

            AcousticBodyProperties sideA = bestFirstMapsToA ? fact.Impactor : fact.Receiver;
            AcousticBodyProperties sideB = bestFirstMapsToA ? fact.Receiver : fact.Impactor;
            bool found = false;
            PhysicalAudioBasisDefinition bestBasis = default;
            float bestDistance = float.MaxValue;
            int count = _definitions.GetPairVariantBasisCandidateCount(bestPair.Id, family.VariantKind);
            for (int i = 0; i < count; i++)
            {
                if (!_definitions.TryGetPairVariantBasisCandidate(bestPair.Id, family.VariantKind, i, out PhysicalAudioBasisDefinition basis))
                    continue;
                if (!TryScorePair(fact, sideA, sideB, family.Weights, basis, out float distance, out PhysicalAudioBasisRejectionReason rejection))
                {
                    trace?.Add(new PhysicalAudioResolveTraceEntry(
                        family.Id,
                        bestPair.Id,
                        family.VariantKind,
                        basis.Id,
                        familyFallbackDepth,
                        bestFirstDepth,
                        bestSecondDepth,
                        false,
                        rejection,
                        0f));
                    continue;
                }
                trace?.Add(new PhysicalAudioResolveTraceEntry(
                    family.Id,
                    bestPair.Id,
                    family.VariantKind,
                    basis.Id,
                    familyFallbackDepth,
                    bestFirstDepth,
                    bestSecondDepth,
                    true,
                    PhysicalAudioBasisRejectionReason.None,
                    distance));
                if (!found
                    || basis.Priority > bestBasis.Priority
                    || (basis.Priority == bestBasis.Priority && distance < bestDistance)
                    || (basis.Priority == bestBasis.Priority && NearlyEqual(distance, bestDistance) && basis.Id.Value < bestBasis.Id.Value))
                {
                    found = true;
                    bestBasis = basis;
                    bestDistance = distance;
                }
            }
            result = found
                ? new ResolvedCandidate(
                    family,
                    bestBasis,
                    familyFallbackDepth,
                    bestDistance,
                    bestFirstDepth > 0 || bestSecondDepth > 0)
                : default;
            return found;
        }

        private static bool IsBetterPairFallback(
            int firstDepth,
            int secondDepth,
            int pairId,
            int bestFirstDepth,
            int bestSecondDepth,
            int bestPairId)
        {
            int total = firstDepth + secondDepth;
            int bestTotal = bestFirstDepth + bestSecondDepth;
            if (total != bestTotal)
                return total < bestTotal;
            int maximum = firstDepth > secondDepth ? firstDepth : secondDepth;
            int bestMaximum = bestFirstDepth > bestSecondDepth ? bestFirstDepth : bestSecondDepth;
            if (maximum != bestMaximum)
                return maximum < bestMaximum;
            return pairId < bestPairId;
        }

        private static bool TryScorePair(
            in PhysicalAudioInteractionFact fact,
            in AcousticBodyProperties sideA,
            in AcousticBodyProperties sideB,
            in PhysicalAudioSelectionWeights weights,
            in PhysicalAudioBasisDefinition basis,
            out float distance,
            out PhysicalAudioBasisRejectionReason rejection)
        {
            distance = 0f;
            if (!TryScoreBody(sideA, basis.SideA, weights, true, false, ref distance, out rejection))
                return false;
            if (!TryScoreBody(sideB, basis.SideB, weights, false, false, ref distance, out rejection))
                return false;
            return TryScoreInteraction(fact, weights, basis, ref distance, out rejection);
        }

        private static bool TryScore(
            in PhysicalAudioInteractionFact fact,
            in PhysicalAudioSelectionWeights weights,
            in PhysicalAudioBasisDefinition basis,
            out float distance,
            out PhysicalAudioBasisRejectionReason rejection)
        {
            distance = 0f;
            if (!TryScoreBody(fact.Impactor, basis.Impactor, weights, true, true, ref distance, out rejection))
                return false;
            if (!TryScoreBody(fact.Receiver, basis.Receiver, weights, false, true, ref distance, out rejection))
                return false;
            return TryScoreInteraction(fact, weights, basis, ref distance, out rejection);
        }

        private static bool TryScoreInteraction(
            in PhysicalAudioInteractionFact fact,
            in PhysicalAudioSelectionWeights weights,
            in PhysicalAudioBasisDefinition basis,
            ref float distance,
            out PhysicalAudioBasisRejectionReason rejection)
        {
            if (!basis.RelativeSpeed.TryGetNormalizedDistance(fact.RelativeSpeed, out float speedDistance))
            {
                rejection = PhysicalAudioBasisRejectionReason.RelativeSpeed;
                return false;
            }
            if (!basis.Intensity.TryGetNormalizedDistance(fact.Intensity, out float intensityDistance))
            {
                rejection = PhysicalAudioBasisRejectionReason.Intensity;
                return false;
            }
            if (!basis.AngleDegrees.TryGetNormalizedDistance(fact.AngleDegrees, out float angleDistance))
            {
                rejection = PhysicalAudioBasisRejectionReason.Angle;
                return false;
            }
            distance += speedDistance * weights.Speed;
            distance += intensityDistance * weights.Intensity;
            distance += angleDistance * weights.Angle;
            rejection = PhysicalAudioBasisRejectionReason.None;
            return true;
        }

        private static bool TryScoreBody(
            in AcousticBodyProperties body,
            in PhysicalAudioBodyAnchorDefinition anchor,
            in PhysicalAudioSelectionWeights weights,
            bool impactor,
            bool checkMaterial,
            ref float distance,
            out PhysicalAudioBasisRejectionReason rejection)
        {
            if (checkMaterial && anchor.MaterialId.IsValid && anchor.MaterialId != body.MaterialId)
            {
                rejection = impactor ? PhysicalAudioBasisRejectionReason.ImpactorMaterial : PhysicalAudioBasisRejectionReason.ReceiverMaterial;
                return false;
            }
            if (!anchor.Mass.TryGetNormalizedDistance(body.Mass, out float massDistance))
            {
                rejection = impactor ? PhysicalAudioBasisRejectionReason.ImpactorMass : PhysicalAudioBasisRejectionReason.ReceiverMass;
                return false;
            }
            if (!anchor.ContactArea.TryGetNormalizedDistance(body.ContactArea, out float areaDistance))
            {
                rejection = impactor ? PhysicalAudioBasisRejectionReason.ImpactorContactArea : PhysicalAudioBasisRejectionReason.ReceiverContactArea;
                return false;
            }
            if (anchor.Surface != AcousticSurfaceKind.Any && anchor.Surface != body.Surface)
            {
                rejection = impactor ? PhysicalAudioBasisRejectionReason.ImpactorSurface : PhysicalAudioBasisRejectionReason.ReceiverSurface;
                return false;
            }
            if (anchor.Construction != AcousticConstructionKind.Any && anchor.Construction != body.Construction)
            {
                rejection = impactor ? PhysicalAudioBasisRejectionReason.ImpactorConstruction : PhysicalAudioBasisRejectionReason.ReceiverConstruction;
                return false;
            }
            distance += (anchor.MaterialId.IsValid ? 0f : 0.5f) * weights.Material;
            distance += massDistance * weights.Mass;
            distance += areaDistance * weights.ContactArea;
            distance += (anchor.Surface == AcousticSurfaceKind.Any ? 0.5f : 0f) * weights.Surface;
            distance += (anchor.Construction == AcousticConstructionKind.Any ? 0.5f : 0f) * weights.Construction;
            rejection = PhysicalAudioBasisRejectionReason.None;
            return true;
        }

        private void EvaluateResponse(
            in PhysicalAudioInteractionFact fact,
            in PhysicalAudioResponseProfileDefinition profile,
            Span<AudioParameterValue> destination)
        {
            _definitions.TryGetMaterial(fact.Impactor.MaterialId, out AcousticMaterialProfileDefinition impactorMaterial);
            _definitions.TryGetMaterial(fact.Receiver.MaterialId, out AcousticMaterialProfileDefinition receiverMaterial);
            for (int i = 0; i < profile.CurveCount; i++)
            {
                PhysicalAudioResponseCurveDefinition curve = profile.GetCurve(i);
                destination[i] = new AudioParameterValue(curve.ParameterId, curve.Evaluate(GetResponseInput(
                    curve.Input,
                    fact,
                    impactorMaterial,
                    receiverMaterial)));
            }
        }

        private static float GetResponseInput(
            PhysicalAudioResponseInput input,
            in PhysicalAudioInteractionFact fact,
            in AcousticMaterialProfileDefinition impactorMaterial,
            in AcousticMaterialProfileDefinition receiverMaterial)
        {
            switch (input)
            {
                case PhysicalAudioResponseInput.EffectiveEnergy:
                    float impactorMass = fact.Impactor.Mass;
                    float receiverMass = fact.Receiver.Mass;
                    float reducedMass = receiverMass > 0f
                        ? (impactorMass * receiverMass) / (impactorMass + receiverMass)
                        : impactorMass;
                    return 0.5f * reducedMass * fact.RelativeSpeed * fact.RelativeSpeed;
                case PhysicalAudioResponseInput.RelativeSpeed:
                    return fact.RelativeSpeed;
                case PhysicalAudioResponseInput.NormalSpeed:
                    return fact.NormalSpeed;
                case PhysicalAudioResponseInput.TangentialRatio:
                    float speedSum = fact.NormalSpeed + fact.TangentialSpeed;
                    return speedSum <= 0.000001f ? 0f : fact.TangentialSpeed / speedSum;
                case PhysicalAudioResponseInput.Pressure:
                    return fact.Pressure;
                case PhysicalAudioResponseInput.Impulse:
                    return fact.Impulse;
                case PhysicalAudioResponseInput.Intensity:
                    return fact.Intensity;
                case PhysicalAudioResponseInput.AngleDegrees:
                    return fact.AngleDegrees;
                case PhysicalAudioResponseInput.ImpactorHardness:
                    return impactorMaterial.Hardness;
                case PhysicalAudioResponseInput.ReceiverHardness:
                    return receiverMaterial.Hardness;
                case PhysicalAudioResponseInput.CombinedDamping:
                    return (impactorMaterial.Damping + receiverMaterial.Damping) * 0.5f;
                default:
                    return 0f;
            }
        }

        private static bool IsBetter(in ResolvedCandidate candidate, in ResolvedCandidate best)
        {
            if (candidate.Family.Priority != best.Family.Priority)
                return candidate.Family.Priority > best.Family.Priority;
            if (candidate.Basis.Priority != best.Basis.Priority)
                return candidate.Basis.Priority > best.Basis.Priority;
            if (!NearlyEqual(candidate.MatchDistance, best.MatchDistance))
                return candidate.MatchDistance < best.MatchDistance;
            return candidate.Basis.Id.Value < best.Basis.Id.Value;
        }

        private static bool NearlyEqual(float left, float right)
        {
            float delta = left - right;
            return delta < 0f ? -delta <= 0.000001f : delta <= 0.000001f;
        }

        private readonly struct ResolvedCandidate
        {
            public ResolvedCandidate(
                in PhysicalAudioFamilyDefinition family,
                in PhysicalAudioBasisDefinition basis,
                int fallbackDepth,
                float matchDistance,
                bool forcedFallback)
            {
                Family = family;
                Basis = basis;
                FallbackDepth = fallbackDepth;
                MatchDistance = matchDistance;
                ForcedFallback = forcedFallback;
            }

            public PhysicalAudioFamilyDefinition Family { get; }
            public PhysicalAudioBasisDefinition Basis { get; }
            public int FallbackDepth { get; }
            public float MatchDistance { get; }
            public bool ForcedFallback { get; }
        }

    }
}

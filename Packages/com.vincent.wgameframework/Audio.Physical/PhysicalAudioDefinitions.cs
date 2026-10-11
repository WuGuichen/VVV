using System;

namespace MxFramework.Audio.Physical
{
    /// <summary>Inclusive accepted range with a preferred anchor used for normalized scoring.</summary>
    /// <remarks>NoAlloc.</remarks>
    public readonly struct PhysicalAudioRange
    {
        public PhysicalAudioRange(float min, float max, float anchor)
        {
            if (float.IsNaN(min) || float.IsNaN(max) || float.IsNaN(anchor) || min > max || anchor < min || anchor > max)
                throw new ArgumentException("Physical audio range requires min <= anchor <= max.");
            Min = min;
            Max = max;
            Anchor = anchor;
        }

        public float Min { get; }

        public float Max { get; }

        public float Anchor { get; }

        /// <remarks>NoAlloc.</remarks>
        public bool TryGetNormalizedDistance(float value, out float distance)
        {
            if (float.IsNaN(value) || value < Min || value > Max)
            {
                distance = 0f;
                return false;
            }

            float span = value <= Anchor ? Anchor - Min : Max - Anchor;
            distance = span <= 0.000001f ? 0f : Abs(value - Anchor) / span;
            return true;
        }

        private static float Abs(float value) => value < 0f ? -value : value;
    }

    /// <summary>Normalized acoustic features for one stable material id.</summary>
    /// <remarks>NoAlloc.</remarks>
    public readonly struct AcousticMaterialProfileDefinition
    {
        public AcousticMaterialProfileDefinition(
            AcousticMaterialId id,
            float hardness,
            float density,
            float roughness,
            float damping,
            float resonance,
            AcousticMaterialId fallbackMaterialId = default)
        {
            if (!id.IsValid)
                throw new ArgumentOutOfRangeException(nameof(id), "Acoustic material id must be positive.");
            Id = id;
            Hardness = Clamp01(hardness);
            Density = Clamp01(density);
            Roughness = Clamp01(roughness);
            Damping = Clamp01(damping);
            Resonance = Clamp01(resonance);
            FallbackMaterialId = fallbackMaterialId;
        }

        public AcousticMaterialId Id { get; }

        public float Hardness { get; }

        public float Density { get; }

        public float Roughness { get; }

        public float Damping { get; }

        public float Resonance { get; }

        /// <summary>Explicit material fallback used only when a Pair Variant is unavailable.</summary>
        public AcousticMaterialId FallbackMaterialId { get; }

        private static float Clamp01(float value)
        {
            if (float.IsNaN(value) || value <= 0f)
                return 0f;
            return value >= 1f ? 1f : value;
        }
    }

    /// <summary>One canonical unordered material pair.</summary>
    /// <remarks>NoAlloc.</remarks>
    public readonly struct AcousticMaterialPairDefinition
    {
        public AcousticMaterialPairDefinition(
            AcousticMaterialPairId id,
            AcousticMaterialId materialAId,
            AcousticMaterialId materialBId)
        {
            if (!id.IsValid)
                throw new ArgumentOutOfRangeException(nameof(id), "Acoustic material Pair id must be positive.");
            if (!materialAId.IsValid || !materialBId.IsValid)
                throw new ArgumentException("Acoustic material Pair requires two valid materials.");
            if (materialAId.Value > materialBId.Value)
                throw new ArgumentException("Acoustic material Pair materials must use canonical ascending order.");
            Id = id;
            MaterialAId = materialAId;
            MaterialBId = materialBId;
        }

        public AcousticMaterialPairId Id { get; }
        public AcousticMaterialId MaterialAId { get; }
        public AcousticMaterialId MaterialBId { get; }
    }

    /// <summary>Selection anchor for one side of a Basis Sound.</summary>
    /// <remarks>NoAlloc.</remarks>
    public readonly struct PhysicalAudioBodyAnchorDefinition
    {
        public PhysicalAudioBodyAnchorDefinition(
            AcousticMaterialId materialId,
            in PhysicalAudioRange mass,
            in PhysicalAudioRange contactArea,
            AcousticSurfaceKind surface,
            AcousticConstructionKind construction)
        {
            MaterialId = materialId;
            Mass = mass;
            ContactArea = contactArea;
            Surface = surface;
            Construction = construction;
        }

        /// <summary>None acts as an explicit wildcard anchor.</summary>
        public AcousticMaterialId MaterialId { get; }

        /// <summary>Project-normalized mass range.</summary>
        public PhysicalAudioRange Mass { get; }

        public PhysicalAudioRange ContactArea { get; }

        /// <summary>Any acts as an explicit wildcard anchor.</summary>
        public AcousticSurfaceKind Surface { get; }

        /// <summary>Any acts as an explicit wildcard anchor.</summary>
        public AcousticConstructionKind Construction { get; }
    }

    /// <summary>Weights used to compare valid Basis Sound anchors.</summary>
    /// <remarks>NoAlloc.</remarks>
    public readonly struct PhysicalAudioSelectionWeights
    {
        public PhysicalAudioSelectionWeights(
            float material,
            float mass,
            float contactArea,
            float surface,
            float construction,
            float speed,
            float intensity,
            float angle)
        {
            Material = NonNegative(material);
            Mass = NonNegative(mass);
            ContactArea = NonNegative(contactArea);
            Surface = NonNegative(surface);
            Construction = NonNegative(construction);
            Speed = NonNegative(speed);
            Intensity = NonNegative(intensity);
            Angle = NonNegative(angle);
        }

        public float Material { get; }
        public float Mass { get; }
        public float ContactArea { get; }
        public float Surface { get; }
        public float Construction { get; }
        public float Speed { get; }
        public float Intensity { get; }
        public float Angle { get; }

        public float Total => Material + Mass + ContactArea + Surface + Construction + Speed + Intensity + Angle;

        private static float NonNegative(float value) => float.IsNaN(value) || value < 0f ? 0f : value;
    }

    /// <summary>Runtime family selecting related Basis Sounds and response behavior.</summary>
    /// <remarks>NoAlloc.</remarks>
    public readonly struct PhysicalAudioFamilyDefinition
    {
        public PhysicalAudioFamilyDefinition(
            PhysicalAudioFamilyId id,
            PhysicalAudioInteractionKind interactionKind,
            PhysicalAudioInteractionMode interactionMode,
            PhysicalAudioFamilyId fallbackFamilyId,
            AudioBasisId fallbackBasisId,
            PhysicalAudioResponseProfileId responseProfileId,
            int priority,
            int cooldownFrames,
            in PhysicalAudioSelectionWeights weights)
            : this(
                id,
                interactionKind,
                interactionMode,
                VariantFromMode(interactionMode),
                fallbackFamilyId,
                fallbackBasisId,
                responseProfileId,
                priority,
                cooldownFrames,
                weights)
        {
        }

        public PhysicalAudioFamilyDefinition(
            PhysicalAudioFamilyId id,
            PhysicalAudioInteractionKind interactionKind,
            PhysicalAudioInteractionMode interactionMode,
            PhysicalAudioVariantKind variantKind,
            PhysicalAudioFamilyId fallbackFamilyId,
            AudioBasisId fallbackBasisId,
            PhysicalAudioResponseProfileId responseProfileId,
            int priority,
            int cooldownFrames,
            in PhysicalAudioSelectionWeights weights)
        {
            if (!id.IsValid)
                throw new ArgumentOutOfRangeException(nameof(id), "Physical audio family id must be positive.");
            if (interactionKind == PhysicalAudioInteractionKind.Unknown)
                throw new ArgumentOutOfRangeException(nameof(interactionKind), "Physical audio family interaction kind must be known.");
            if (cooldownFrames < 0)
                throw new ArgumentOutOfRangeException(nameof(cooldownFrames), "Physical audio family cooldown cannot be negative.");
            if (variantKind == PhysicalAudioVariantKind.Unknown)
                throw new ArgumentOutOfRangeException(nameof(variantKind), "Physical audio family variant must be known.");
            Id = id;
            InteractionKind = interactionKind;
            InteractionMode = interactionMode;
            VariantKind = variantKind;
            FallbackFamilyId = fallbackFamilyId;
            FallbackBasisId = fallbackBasisId;
            ResponseProfileId = responseProfileId;
            Priority = priority;
            CooldownFrames = cooldownFrames;
            Weights = weights.Total > 0f
                ? weights
                : new PhysicalAudioSelectionWeights(4f, 1f, 1f, 0.5f, 0.5f, 1f, 1f, 0.5f);
        }

        public PhysicalAudioFamilyId Id { get; }
        public PhysicalAudioInteractionKind InteractionKind { get; }
        public PhysicalAudioInteractionMode InteractionMode { get; }
        public PhysicalAudioVariantKind VariantKind { get; }
        public PhysicalAudioFamilyId FallbackFamilyId { get; }
        public AudioBasisId FallbackBasisId { get; }
        public PhysicalAudioResponseProfileId ResponseProfileId { get; }
        public int Priority { get; }
        public int CooldownFrames { get; }
        public PhysicalAudioSelectionWeights Weights { get; }

        private static PhysicalAudioVariantKind VariantFromMode(PhysicalAudioInteractionMode mode)
        {
            switch (mode)
            {
                case PhysicalAudioInteractionMode.Impact: return PhysicalAudioVariantKind.Impact;
                case PhysicalAudioInteractionMode.Step: return PhysicalAudioVariantKind.Step;
                case PhysicalAudioInteractionMode.Landing: return PhysicalAudioVariantKind.Landing;
                case PhysicalAudioInteractionMode.Hit: return PhysicalAudioVariantKind.Hit;
                case PhysicalAudioInteractionMode.Block: return PhysicalAudioVariantKind.Block;
                case PhysicalAudioInteractionMode.Sweep: return PhysicalAudioVariantKind.Sweep;
                case PhysicalAudioInteractionMode.Scrape: return PhysicalAudioVariantKind.Scrape;
                case PhysicalAudioInteractionMode.Roll: return PhysicalAudioVariantKind.Roll;
                case PhysicalAudioInteractionMode.Compression: return PhysicalAudioVariantKind.Compression;
                default: return PhysicalAudioVariantKind.Impact;
            }
        }
    }

    /// <summary>A selectable complete Basis Sound referencing one runtime Audio Cue.</summary>
    /// <remarks>NoAlloc.</remarks>
    public readonly struct PhysicalAudioBasisDefinition
    {
        public PhysicalAudioBasisDefinition(
            AudioBasisId id,
            PhysicalAudioFamilyId familyId,
            int cueId,
            int priority,
            in PhysicalAudioBodyAnchorDefinition impactor,
            in PhysicalAudioBodyAnchorDefinition receiver,
            in PhysicalAudioRange relativeSpeed,
            in PhysicalAudioRange intensity,
            in PhysicalAudioRange angleDegrees)
        {
            if (!id.IsValid)
                throw new ArgumentOutOfRangeException(nameof(id), "Physical audio Basis id must be positive.");
            if (!familyId.IsValid)
                throw new ArgumentOutOfRangeException(nameof(familyId), "Physical audio Basis family id must be positive.");
            if (cueId <= 0)
                throw new ArgumentOutOfRangeException(nameof(cueId), "Physical audio Basis Cue id must be positive.");
            Id = id;
            FamilyId = familyId;
            CueId = cueId;
            Priority = priority;
            Impactor = impactor;
            Receiver = receiver;
            RelativeSpeed = relativeSpeed;
            Intensity = intensity;
            AngleDegrees = angleDegrees;
            MaterialPairId = AcousticMaterialPairId.None;
            VariantKind = PhysicalAudioVariantKind.Unknown;
            SideA = impactor;
            SideB = receiver;
        }

        public PhysicalAudioBasisDefinition(
            AudioBasisId id,
            AcousticMaterialPairId materialPairId,
            PhysicalAudioVariantKind variantKind,
            int cueId,
            int priority,
            in PhysicalAudioBodyAnchorDefinition sideA,
            in PhysicalAudioBodyAnchorDefinition sideB,
            in PhysicalAudioRange relativeSpeed,
            in PhysicalAudioRange intensity,
            in PhysicalAudioRange angleDegrees)
        {
            if (!id.IsValid)
                throw new ArgumentOutOfRangeException(nameof(id), "Physical audio Basis id must be positive.");
            if (!materialPairId.IsValid)
                throw new ArgumentOutOfRangeException(nameof(materialPairId), "Physical audio Basis Pair id must be positive.");
            if (variantKind == PhysicalAudioVariantKind.Unknown)
                throw new ArgumentOutOfRangeException(nameof(variantKind), "Physical audio Basis variant must be known.");
            if (cueId <= 0)
                throw new ArgumentOutOfRangeException(nameof(cueId), "Physical audio Basis Cue id must be positive.");
            Id = id;
            FamilyId = PhysicalAudioFamilyId.None;
            MaterialPairId = materialPairId;
            VariantKind = variantKind;
            CueId = cueId;
            Priority = priority;
            SideA = sideA;
            SideB = sideB;
            Impactor = sideA;
            Receiver = sideB;
            RelativeSpeed = relativeSpeed;
            Intensity = intensity;
            AngleDegrees = angleDegrees;
        }

        public AudioBasisId Id { get; }
        public PhysicalAudioFamilyId FamilyId { get; }
        public AcousticMaterialPairId MaterialPairId { get; }
        public PhysicalAudioVariantKind VariantKind { get; }
        public int CueId { get; }
        public int Priority { get; }
        public PhysicalAudioBodyAnchorDefinition Impactor { get; }
        public PhysicalAudioBodyAnchorDefinition Receiver { get; }
        public PhysicalAudioBodyAnchorDefinition SideA { get; }
        public PhysicalAudioBodyAnchorDefinition SideB { get; }
        public PhysicalAudioRange RelativeSpeed { get; }
        public PhysicalAudioRange Intensity { get; }
        public PhysicalAudioRange AngleDegrees { get; }
    }

    public enum PhysicalAudioResponseInput
    {
        EffectiveEnergy = 0,
        RelativeSpeed = 1,
        NormalSpeed = 2,
        TangentialRatio = 3,
        Pressure = 4,
        Impulse = 5,
        Intensity = 6,
        AngleDegrees = 7,
        ImpactorHardness = 8,
        ReceiverHardness = 9,
        CombinedDamping = 10
    }

    /// <summary>One immutable point in a physical-audio response curve.</summary>
    /// <remarks>NoAlloc.</remarks>
    public readonly struct PhysicalAudioResponsePoint
    {
        public PhysicalAudioResponsePoint(float input, float output)
        {
            if (float.IsNaN(input) || float.IsNaN(output))
                throw new ArgumentException("Physical audio response point cannot contain NaN.");
            Input = input;
            Output = output;
        }

        public float Input { get; }
        public float Output { get; }
    }

    /// <summary>Piecewise-linear response for one Audio parameter.</summary>
    /// <remarks>NoAlloc when evaluating. Constructor is LowFreqAlloc and clones points.</remarks>
    public readonly struct PhysicalAudioResponseCurveDefinition
    {
        private readonly PhysicalAudioResponsePoint[] _points;

        public PhysicalAudioResponseCurveDefinition(
            int parameterId,
            PhysicalAudioResponseInput input,
            float outputMin,
            float outputMax,
            PhysicalAudioResponsePoint[] points)
        {
            if (parameterId <= 0)
                throw new ArgumentOutOfRangeException(nameof(parameterId), "Physical audio response parameter id must be positive.");
            if (float.IsNaN(outputMin) || float.IsNaN(outputMax) || outputMin > outputMax)
                throw new ArgumentException("Physical audio response output range is invalid.");
            if (points == null || points.Length == 0)
                throw new ArgumentException("Physical audio response curve requires at least one point.", nameof(points));
            _points = new PhysicalAudioResponsePoint[points.Length];
            Array.Copy(points, _points, points.Length);
            for (int i = 1; i < _points.Length; i++)
            {
                if (_points[i].Input <= _points[i - 1].Input)
                    throw new ArgumentException("Physical audio response inputs must be strictly increasing.", nameof(points));
            }
            ParameterId = parameterId;
            Input = input;
            OutputMin = outputMin;
            OutputMax = outputMax;
        }

        public int ParameterId { get; }
        public PhysicalAudioResponseInput Input { get; }
        public float OutputMin { get; }
        public float OutputMax { get; }
        public int PointCount => _points == null ? 0 : _points.Length;

        /// <remarks>NoAlloc.</remarks>
        public float Evaluate(float input)
        {
            if (_points == null || _points.Length == 0)
                return OutputMin;
            float output;
            if (input <= _points[0].Input)
            {
                output = _points[0].Output;
            }
            else if (input >= _points[_points.Length - 1].Input)
            {
                output = _points[_points.Length - 1].Output;
            }
            else
            {
                int upper = 1;
                while (input > _points[upper].Input)
                    upper++;
                PhysicalAudioResponsePoint left = _points[upper - 1];
                PhysicalAudioResponsePoint right = _points[upper];
                float rate = (input - left.Input) / (right.Input - left.Input);
                output = left.Output + ((right.Output - left.Output) * rate);
            }
            if (output <= OutputMin)
                return OutputMin;
            return output >= OutputMax ? OutputMax : output;
        }
    }

    /// <summary>Response curves applied after a Basis Sound is selected.</summary>
    /// <remarks>LowFreqAlloc during construction; curve access and evaluation are NoAlloc.</remarks>
    public readonly struct PhysicalAudioResponseProfileDefinition
    {
        private readonly PhysicalAudioResponseCurveDefinition[] _curves;

        public PhysicalAudioResponseProfileDefinition(
            PhysicalAudioResponseProfileId id,
            PhysicalAudioResponseCurveDefinition[] curves)
        {
            if (!id.IsValid)
                throw new ArgumentOutOfRangeException(nameof(id), "Physical audio response profile id must be positive.");
            Id = id;
            _curves = curves == null || curves.Length == 0
                ? Array.Empty<PhysicalAudioResponseCurveDefinition>()
                : (PhysicalAudioResponseCurveDefinition[])curves.Clone();
        }

        public PhysicalAudioResponseProfileId Id { get; }
        public int CurveCount => _curves == null ? 0 : _curves.Length;

        /// <remarks>NoAlloc.</remarks>
        public PhysicalAudioResponseCurveDefinition GetCurve(int index)
        {
            if ((uint)index >= (uint)CurveCount)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _curves[index];
        }
    }

    /// <summary>Startup-built provider for physical-audio runtime definitions.</summary>
    public interface IPhysicalAudioDefinitionProvider
    {
        string PhysicalManifestHash { get; }
        string AudioCatalogHash { get; }
        int MaterialCount { get; }
        int FamilyCount { get; }
        int MaterialPairCount { get; }
        int BasisCount { get; }
        int MaxResponseParameterCount { get; }

        /// <summary>Returns one startup-stable Basis definition by dense catalog ordinal.</summary>
        /// <remarks>NoAlloc.</remarks>
        PhysicalAudioBasisDefinition GetBasisAt(int index);

        /// <summary>Returns one startup-stable Family definition by dense catalog ordinal.</summary>
        /// <remarks>NoAlloc.</remarks>
        PhysicalAudioFamilyDefinition GetFamilyAt(int index);

        /// <summary>Returns one startup-stable unordered Pair definition.</summary>
        /// <remarks>NoAlloc.</remarks>
        AcousticMaterialPairDefinition GetMaterialPairAt(int index);

        /// <remarks>NoAlloc.</remarks>
        bool TryGetMaterial(AcousticMaterialId id, out AcousticMaterialProfileDefinition definition);

        /// <remarks>NoAlloc.</remarks>
        bool TryGetMaterialPair(AcousticMaterialPairId id, out AcousticMaterialPairDefinition definition);

        /// <summary>Finds the canonical unordered Pair for two directed fact materials.</summary>
        /// <remarks>NoAlloc.</remarks>
        bool TryFindMaterialPair(AcousticMaterialId first, AcousticMaterialId second, out AcousticMaterialPairDefinition definition);

        /// <remarks>NoAlloc.</remarks>
        bool TryGetFamily(PhysicalAudioFamilyId id, out PhysicalAudioFamilyDefinition definition);

        /// <remarks>NoAlloc.</remarks>
        bool TryGetBasis(AudioBasisId id, out PhysicalAudioBasisDefinition definition);

        /// <remarks>NoAlloc.</remarks>
        bool TryGetResponseProfile(PhysicalAudioResponseProfileId id, out PhysicalAudioResponseProfileDefinition definition);

        /// <remarks>NoAlloc.</remarks>
        int GetFamilyCandidateCount(PhysicalAudioInteractionKind kind, PhysicalAudioInteractionMode mode);

        /// <remarks>NoAlloc.</remarks>
        bool TryGetFamilyCandidate(PhysicalAudioInteractionKind kind, PhysicalAudioInteractionMode mode, int index, out PhysicalAudioFamilyDefinition definition);

        /// <remarks>NoAlloc.</remarks>
        int GetBasisCandidateCount(PhysicalAudioFamilyId familyId);

        /// <remarks>NoAlloc.</remarks>
        bool TryGetBasisCandidate(PhysicalAudioFamilyId familyId, int index, out PhysicalAudioBasisDefinition definition);

        /// <remarks>NoAlloc.</remarks>
        int GetPairVariantBasisCandidateCount(AcousticMaterialPairId pairId, PhysicalAudioVariantKind variantKind);

        /// <remarks>NoAlloc.</remarks>
        bool TryGetPairVariantBasisCandidate(
            AcousticMaterialPairId pairId,
            PhysicalAudioVariantKind variantKind,
            int index,
            out PhysicalAudioBasisDefinition definition);
    }

    /// <summary>Immutable-after-construction physical-audio runtime catalog.</summary>
    /// <remarks>LowFreqAlloc during startup construction. All query methods are NoAlloc.</remarks>
    public sealed class PhysicalAudioDefinitionCatalog : IPhysicalAudioDefinitionProvider
    {
        private const int InteractionKindCount = (int)PhysicalAudioInteractionKind.Compression + 1;
        private const int InteractionModeCount = (int)PhysicalAudioInteractionMode.Compression + 1;
        private const int VariantKindCount = (int)PhysicalAudioVariantKind.Compression + 1;

        private readonly AcousticMaterialProfileDefinition[] _materialDefinitions;
        private readonly PhysicalAudioFamilyDefinition[] _familyDefinitions;
        private readonly AcousticMaterialPairDefinition[] _materialPairDefinitions;
        private readonly PhysicalAudioBasisDefinition[] _basisDefinitions;
        private readonly PhysicalAudioResponseProfileDefinition[] _responseDefinitions;
        private readonly AcousticMaterialProfileDefinition[] _materialsById;
        private readonly PhysicalAudioFamilyDefinition[] _familiesById;
        private readonly AcousticMaterialPairDefinition[] _materialPairsById;
        private readonly AcousticMaterialPairDefinition[] _materialPairsByMaterials;
        private readonly PhysicalAudioBasisDefinition[] _basesById;
        private readonly PhysicalAudioResponseProfileDefinition[] _responsesById;
        private readonly PhysicalAudioFamilyDefinition[] _familyCandidates;
        private readonly CandidateRange[] _familyCandidateRanges;
        private readonly PhysicalAudioBasisDefinition[] _basisCandidates;
        private readonly CandidateRange[] _basisCandidateRanges;
        private readonly PhysicalAudioBasisDefinition[] _pairVariantBasisCandidates;
        private readonly CandidateRange[] _pairVariantBasisCandidateRanges;

        public PhysicalAudioDefinitionCatalog(
            AcousticMaterialProfileDefinition[] materials,
            PhysicalAudioFamilyDefinition[] families,
            PhysicalAudioBasisDefinition[] basisSounds,
            PhysicalAudioResponseProfileDefinition[] responseProfiles,
            string physicalManifestHash = "",
            string audioCatalogHash = "")
            : this(
                materials,
                Array.Empty<AcousticMaterialPairDefinition>(),
                families,
                basisSounds,
                responseProfiles,
                physicalManifestHash,
                audioCatalogHash)
        {
        }

        public PhysicalAudioDefinitionCatalog(
            AcousticMaterialProfileDefinition[] materials,
            AcousticMaterialPairDefinition[] materialPairs,
            PhysicalAudioFamilyDefinition[] families,
            PhysicalAudioBasisDefinition[] basisSounds,
            PhysicalAudioResponseProfileDefinition[] responseProfiles,
            string physicalManifestHash = "",
            string audioCatalogHash = "")
        {
            _materialDefinitions = materials == null ? Array.Empty<AcousticMaterialProfileDefinition>() : (AcousticMaterialProfileDefinition[])materials.Clone();
            _materialPairDefinitions = materialPairs == null ? Array.Empty<AcousticMaterialPairDefinition>() : (AcousticMaterialPairDefinition[])materialPairs.Clone();
            _familyDefinitions = families == null ? Array.Empty<PhysicalAudioFamilyDefinition>() : (PhysicalAudioFamilyDefinition[])families.Clone();
            _basisDefinitions = basisSounds == null ? Array.Empty<PhysicalAudioBasisDefinition>() : (PhysicalAudioBasisDefinition[])basisSounds.Clone();
            _responseDefinitions = responseProfiles == null ? Array.Empty<PhysicalAudioResponseProfileDefinition>() : (PhysicalAudioResponseProfileDefinition[])responseProfiles.Clone();
            PhysicalManifestHash = physicalManifestHash ?? string.Empty;
            AudioCatalogHash = audioCatalogHash ?? string.Empty;

            _materialsById = (AcousticMaterialProfileDefinition[])_materialDefinitions.Clone();
            _materialPairsById = (AcousticMaterialPairDefinition[])_materialPairDefinitions.Clone();
            _materialPairsByMaterials = (AcousticMaterialPairDefinition[])_materialPairDefinitions.Clone();
            _familiesById = (PhysicalAudioFamilyDefinition[])_familyDefinitions.Clone();
            _basesById = (PhysicalAudioBasisDefinition[])_basisDefinitions.Clone();
            _responsesById = (PhysicalAudioResponseProfileDefinition[])_responseDefinitions.Clone();
            Array.Sort(_materialsById, CompareMaterialIds);
            Array.Sort(_materialPairsById, CompareMaterialPairIds);
            Array.Sort(_materialPairsByMaterials, CompareMaterialPairMaterials);
            Array.Sort(_familiesById, CompareFamilyIds);
            Array.Sort(_basesById, CompareBasisIds);
            Array.Sort(_responsesById, CompareResponseIds);
            ValidateUniqueIds();
            ValidateUniqueMaterialPairMaterials(_materialPairsByMaterials);

            for (int i = 0; i < _responsesById.Length; i++)
            {
                PhysicalAudioResponseProfileDefinition profile = _responsesById[i];
                if (profile.CurveCount > MaxResponseParameterCount)
                    MaxResponseParameterCount = profile.CurveCount;
            }
            for (int i = 0; i < _familiesById.Length; i++)
            {
                PhysicalAudioFamilyDefinition family = _familiesById[i];
                if (family.ResponseProfileId.IsValid && FindResponseIndex(family.ResponseProfileId.Value) < 0)
                    throw new ArgumentException("Physical audio family references missing response profile " + family.ResponseProfileId.Value + ".");
            }
            ValidateMaterialFallbacks();
            for (int i = 0; i < _materialPairsById.Length; i++)
            {
                AcousticMaterialPairDefinition pair = _materialPairsById[i];
                if (FindMaterialIndex(pair.MaterialAId.Value) < 0 || FindMaterialIndex(pair.MaterialBId.Value) < 0)
                    throw new ArgumentException("Physical audio Pair references a missing material: " + pair.Id.Value + ".");
            }
            for (int i = 0; i < _basesById.Length; i++)
            {
                PhysicalAudioBasisDefinition basis = _basesById[i];
                if (basis.MaterialPairId.IsValid)
                {
                    if (FindMaterialPairIndex(basis.MaterialPairId.Value) < 0)
                        throw new ArgumentException("Physical audio Basis references missing Pair " + basis.MaterialPairId.Value + ".");
                    if (basis.VariantKind == PhysicalAudioVariantKind.Unknown)
                        throw new ArgumentException("Physical audio Pair Basis requires a Variant.");
                    ValidateMaterialAnchor(basis.SideA);
                    ValidateMaterialAnchor(basis.SideB);
                }
                else
                {
                    if (FindFamilyIndex(basis.FamilyId.Value) < 0)
                        throw new ArgumentException("Physical audio Basis references missing family " + basis.FamilyId.Value + ".");
                    ValidateMaterialAnchor(basis.Impactor);
                    ValidateMaterialAnchor(basis.Receiver);
                }
            }
            ValidateFallbacks();

            _familyCandidates = (PhysicalAudioFamilyDefinition[])_familyDefinitions.Clone();
            Array.Sort(_familyCandidates, CompareFamilyCandidates);
            _familyCandidateRanges = BuildFamilyCandidateRanges(_familyCandidates);
            _basisCandidates = (PhysicalAudioBasisDefinition[])_basisDefinitions.Clone();
            Array.Sort(_basisCandidates, CompareBasisCandidates);
            _basisCandidateRanges = BuildBasisCandidateRanges(_basisCandidates);
            _pairVariantBasisCandidates = BuildPairVariantBasisCandidates(_basisDefinitions);
            _pairVariantBasisCandidateRanges = BuildPairVariantBasisCandidateRanges(_pairVariantBasisCandidates);
        }

        public string PhysicalManifestHash { get; }
        public string AudioCatalogHash { get; }
        public int MaterialCount => _materialDefinitions.Length;
        public int MaterialPairCount => _materialPairDefinitions.Length;
        public int FamilyCount => _familyDefinitions.Length;
        public int BasisCount => _basisDefinitions.Length;
        public int ResponseProfileCount => _responseDefinitions.Length;
        public int MaxResponseParameterCount { get; }

        /// <remarks>NoAlloc.</remarks>
        public AcousticMaterialProfileDefinition GetMaterialAt(int index) => _materialDefinitions[index];

        /// <remarks>NoAlloc.</remarks>
        public AcousticMaterialPairDefinition GetMaterialPairAt(int index) => _materialPairDefinitions[index];

        /// <remarks>NoAlloc.</remarks>
        public PhysicalAudioFamilyDefinition GetFamilyAt(int index) => _familyDefinitions[index];

        /// <remarks>NoAlloc.</remarks>
        public PhysicalAudioBasisDefinition GetBasisAt(int index) => _basisDefinitions[index];

        /// <remarks>NoAlloc.</remarks>
        public PhysicalAudioResponseProfileDefinition GetResponseProfileAt(int index) => _responseDefinitions[index];

        /// <inheritdoc />
        public bool TryGetMaterial(AcousticMaterialId id, out AcousticMaterialProfileDefinition definition)
        {
            int index = FindMaterialIndex(id.Value);
            definition = index >= 0 ? _materialsById[index] : default;
            return index >= 0;
        }

        /// <inheritdoc />
        public bool TryGetMaterialPair(AcousticMaterialPairId id, out AcousticMaterialPairDefinition definition)
        {
            int index = FindMaterialPairIndex(id.Value);
            definition = index >= 0 ? _materialPairsById[index] : default;
            return index >= 0;
        }

        /// <inheritdoc />
        public bool TryFindMaterialPair(AcousticMaterialId first, AcousticMaterialId second, out AcousticMaterialPairDefinition definition)
        {
            int materialA = first.Value <= second.Value ? first.Value : second.Value;
            int materialB = first.Value <= second.Value ? second.Value : first.Value;
            int low = 0;
            int high = _materialPairsByMaterials.Length - 1;
            while (low <= high)
            {
                int middle = low + ((high - low) >> 1);
                AcousticMaterialPairDefinition candidate = _materialPairsByMaterials[middle];
                int compareA = candidate.MaterialAId.Value.CompareTo(materialA);
                int compare = compareA != 0 ? compareA : candidate.MaterialBId.Value.CompareTo(materialB);
                if (compare == 0)
                {
                    definition = candidate;
                    return true;
                }
                if (compare < 0)
                    low = middle + 1;
                else
                    high = middle - 1;
            }
            definition = default;
            return false;
        }

        /// <inheritdoc />
        public bool TryGetFamily(PhysicalAudioFamilyId id, out PhysicalAudioFamilyDefinition definition)
        {
            int index = FindFamilyIndex(id.Value);
            definition = index >= 0 ? _familiesById[index] : default;
            return index >= 0;
        }

        /// <inheritdoc />
        public bool TryGetBasis(AudioBasisId id, out PhysicalAudioBasisDefinition definition)
        {
            int index = FindBasisIndex(id.Value);
            definition = index >= 0 ? _basesById[index] : default;
            return index >= 0;
        }

        /// <inheritdoc />
        public bool TryGetResponseProfile(PhysicalAudioResponseProfileId id, out PhysicalAudioResponseProfileDefinition definition)
        {
            int index = FindResponseIndex(id.Value);
            definition = index >= 0 ? _responsesById[index] : default;
            return index >= 0;
        }

        /// <inheritdoc />
        public int GetFamilyCandidateCount(PhysicalAudioInteractionKind kind, PhysicalAudioInteractionMode mode)
        {
            int key = GetFamilyKeyIndex(kind, mode);
            return key >= 0 ? _familyCandidateRanges[key].Count : 0;
        }

        /// <inheritdoc />
        public bool TryGetFamilyCandidate(PhysicalAudioInteractionKind kind, PhysicalAudioInteractionMode mode, int index, out PhysicalAudioFamilyDefinition definition)
        {
            int key = GetFamilyKeyIndex(kind, mode);
            if (key >= 0)
            {
                CandidateRange range = _familyCandidateRanges[key];
                if ((uint)index < (uint)range.Count)
                {
                    definition = _familyCandidates[range.Start + index];
                    return true;
                }
            }
            definition = default;
            return false;
        }

        /// <inheritdoc />
        public int GetBasisCandidateCount(PhysicalAudioFamilyId familyId)
        {
            int familyIndex = FindFamilyIndex(familyId.Value);
            return familyIndex >= 0 ? _basisCandidateRanges[familyIndex].Count : 0;
        }

        /// <inheritdoc />
        public bool TryGetBasisCandidate(PhysicalAudioFamilyId familyId, int index, out PhysicalAudioBasisDefinition definition)
        {
            int familyIndex = FindFamilyIndex(familyId.Value);
            if (familyIndex >= 0)
            {
                CandidateRange range = _basisCandidateRanges[familyIndex];
                if ((uint)index < (uint)range.Count)
                {
                    definition = _basisCandidates[range.Start + index];
                    return true;
                }
            }
            definition = default;
            return false;
        }

        /// <inheritdoc />
        public int GetPairVariantBasisCandidateCount(AcousticMaterialPairId pairId, PhysicalAudioVariantKind variantKind)
        {
            int pairIndex = FindMaterialPairIndex(pairId.Value);
            int variant = (int)variantKind;
            if (pairIndex < 0 || variant <= 0 || variant >= VariantKindCount)
                return 0;
            return _pairVariantBasisCandidateRanges[(pairIndex * VariantKindCount) + variant].Count;
        }

        /// <inheritdoc />
        public bool TryGetPairVariantBasisCandidate(
            AcousticMaterialPairId pairId,
            PhysicalAudioVariantKind variantKind,
            int index,
            out PhysicalAudioBasisDefinition definition)
        {
            int pairIndex = FindMaterialPairIndex(pairId.Value);
            int variant = (int)variantKind;
            if (pairIndex >= 0 && variant > 0 && variant < VariantKindCount)
            {
                CandidateRange range = _pairVariantBasisCandidateRanges[(pairIndex * VariantKindCount) + variant];
                if ((uint)index < (uint)range.Count)
                {
                    definition = _pairVariantBasisCandidates[range.Start + index];
                    return true;
                }
            }
            definition = default;
            return false;
        }

        private static CandidateRange[] BuildFamilyCandidateRanges(PhysicalAudioFamilyDefinition[] candidates)
        {
            var ranges = new CandidateRange[InteractionKindCount * InteractionModeCount];
            int start = 0;
            while (start < candidates.Length)
            {
                PhysicalAudioFamilyDefinition first = candidates[start];
                int end = start + 1;
                while (end < candidates.Length
                    && candidates[end].InteractionKind == first.InteractionKind
                    && candidates[end].InteractionMode == first.InteractionMode)
                    end++;
                int key = GetFamilyKeyIndex(first.InteractionKind, first.InteractionMode);
                if (key < 0)
                    throw new ArgumentException("Physical audio family interaction kind or mode is outside the runtime range.");
                ranges[key] = new CandidateRange(start, end - start);
                start = end;
            }
            return ranges;
        }

        private CandidateRange[] BuildBasisCandidateRanges(PhysicalAudioBasisDefinition[] candidates)
        {
            var ranges = new CandidateRange[_familiesById.Length];
            int start = 0;
            while (start < candidates.Length)
            {
                int familyId = candidates[start].FamilyId.Value;
                if (familyId <= 0)
                {
                    start++;
                    continue;
                }
                int end = start + 1;
                while (end < candidates.Length && candidates[end].FamilyId.Value == familyId)
                    end++;
                int familyIndex = FindFamilyIndex(familyId);
                if (familyIndex < 0)
                    throw new ArgumentException("Physical audio Basis references missing family " + familyId + ".");
                ranges[familyIndex] = new CandidateRange(start, end - start);
                start = end;
            }
            return ranges;
        }

        private static PhysicalAudioBasisDefinition[] BuildPairVariantBasisCandidates(PhysicalAudioBasisDefinition[] source)
        {
            int count = 0;
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i].MaterialPairId.IsValid && source[i].VariantKind != PhysicalAudioVariantKind.Unknown)
                    count++;
            }
            var result = new PhysicalAudioBasisDefinition[count];
            int destination = 0;
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i].MaterialPairId.IsValid && source[i].VariantKind != PhysicalAudioVariantKind.Unknown)
                    result[destination++] = source[i];
            }
            Array.Sort(result, ComparePairVariantBasisCandidates);
            return result;
        }

        private CandidateRange[] BuildPairVariantBasisCandidateRanges(PhysicalAudioBasisDefinition[] candidates)
        {
            var ranges = new CandidateRange[_materialPairsById.Length * VariantKindCount];
            int start = 0;
            while (start < candidates.Length)
            {
                AcousticMaterialPairId pairId = candidates[start].MaterialPairId;
                PhysicalAudioVariantKind variant = candidates[start].VariantKind;
                int end = start + 1;
                while (end < candidates.Length
                    && candidates[end].MaterialPairId == pairId
                    && candidates[end].VariantKind == variant)
                    end++;
                int pairIndex = FindMaterialPairIndex(pairId.Value);
                if (pairIndex < 0)
                    throw new ArgumentException("Physical audio Basis references missing Pair " + pairId.Value + ".");
                ranges[(pairIndex * VariantKindCount) + (int)variant] = new CandidateRange(start, end - start);
                start = end;
            }
            return ranges;
        }

        private void ValidateFallbacks()
        {
            for (int i = 0; i < _familyDefinitions.Length; i++)
            {
                PhysicalAudioFamilyDefinition family = _familyDefinitions[i];
                if (family.FallbackFamilyId.IsValid && FindFamilyIndex(family.FallbackFamilyId.Value) < 0)
                    throw new ArgumentException("Physical audio family references missing fallback family " + family.FallbackFamilyId.Value + ".");
                if (family.FallbackBasisId.IsValid)
                {
                    if (!TryGetBasis(family.FallbackBasisId, out PhysicalAudioBasisDefinition basis)
                        || (basis.FamilyId.IsValid && basis.FamilyId != family.Id)
                        || (basis.MaterialPairId.IsValid && basis.VariantKind != family.VariantKind))
                        throw new ArgumentException("Physical audio family fallback Basis is missing or belongs to another family: " + family.FallbackBasisId.Value + ".");
                }

                PhysicalAudioFamilyDefinition current = family;
                int depth = 0;
                while (current.FallbackFamilyId.IsValid)
                {
                    if (depth++ >= _familiesById.Length)
                        throw new ArgumentException("Physical audio family fallback chain contains a cycle at family " + family.Id.Value + ".");
                    if (!TryGetFamily(current.FallbackFamilyId, out current))
                        break;
                }
            }
        }

        private void ValidateMaterialFallbacks()
        {
            for (int i = 0; i < _materialsById.Length; i++)
            {
                AcousticMaterialProfileDefinition material = _materialsById[i];
                if (material.FallbackMaterialId.IsValid && FindMaterialIndex(material.FallbackMaterialId.Value) < 0)
                    throw new ArgumentException("Physical audio material references missing fallback material " + material.FallbackMaterialId.Value + ".");
                AcousticMaterialProfileDefinition current = material;
                int depth = 0;
                while (current.FallbackMaterialId.IsValid)
                {
                    if (depth++ >= _materialsById.Length)
                        throw new ArgumentException("Physical audio material fallback chain contains a cycle at material " + material.Id.Value + ".");
                    if (!TryGetMaterial(current.FallbackMaterialId, out current))
                        break;
                }
            }
        }

        private void ValidateMaterialAnchor(in PhysicalAudioBodyAnchorDefinition anchor)
        {
            if (anchor.MaterialId.IsValid && FindMaterialIndex(anchor.MaterialId.Value) < 0)
                throw new ArgumentException("Physical audio Basis references missing material " + anchor.MaterialId.Value + ".");
        }

        private void ValidateUniqueIds()
        {
            ValidateUniqueMaterialIds(_materialsById);
            ValidateUniqueMaterialPairIds(_materialPairsById);
            ValidateUniqueFamilyIds(_familiesById);
            ValidateUniqueBasisIds(_basesById);
            ValidateUniqueResponseIds(_responsesById);
        }

        private static void ValidateUniqueMaterialPairIds(AcousticMaterialPairDefinition[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                int id = values[i].Id.Value;
                if (id <= 0 || (i > 0 && values[i - 1].Id.Value == id))
                    throw new ArgumentException("Physical audio material Pair id is invalid or duplicated: " + id + ".");
            }
        }

        private static void ValidateUniqueMaterialPairMaterials(AcousticMaterialPairDefinition[] values)
        {
            for (int i = 1; i < values.Length; i++)
            {
                AcousticMaterialPairDefinition previous = values[i - 1];
                AcousticMaterialPairDefinition current = values[i];
                if (previous.MaterialAId == current.MaterialAId && previous.MaterialBId == current.MaterialBId)
                    throw new ArgumentException(
                        "Physical audio material Pair is duplicated: "
                        + current.MaterialAId.Value
                        + " x "
                        + current.MaterialBId.Value
                        + ".");
            }
        }

        private static void ValidateUniqueMaterialIds(AcousticMaterialProfileDefinition[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                int id = values[i].Id.Value;
                if (id <= 0 || (i > 0 && values[i - 1].Id.Value == id))
                    throw new ArgumentException("Physical audio material id is invalid or duplicated: " + id + ".");
            }
        }

        private static void ValidateUniqueFamilyIds(PhysicalAudioFamilyDefinition[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                int id = values[i].Id.Value;
                if (id <= 0 || (i > 0 && values[i - 1].Id.Value == id))
                    throw new ArgumentException("Physical audio family id is invalid or duplicated: " + id + ".");
            }
        }

        private static void ValidateUniqueBasisIds(PhysicalAudioBasisDefinition[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                int id = values[i].Id.Value;
                if (id <= 0 || (i > 0 && values[i - 1].Id.Value == id))
                    throw new ArgumentException("Physical audio Basis Sound id is invalid or duplicated: " + id + ".");
            }
        }

        private static void ValidateUniqueResponseIds(PhysicalAudioResponseProfileDefinition[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                int id = values[i].Id.Value;
                if (id <= 0 || (i > 0 && values[i - 1].Id.Value == id))
                    throw new ArgumentException("Physical audio response profile id is invalid or duplicated: " + id + ".");
            }
        }

        private int FindMaterialIndex(int id) => BinarySearchMaterial(_materialsById, id);
        private int FindMaterialPairIndex(int id) => BinarySearchMaterialPair(_materialPairsById, id);
        private int FindFamilyIndex(int id) => BinarySearchFamily(_familiesById, id);
        private int FindBasisIndex(int id) => BinarySearchBasis(_basesById, id);
        private int FindResponseIndex(int id) => BinarySearchResponse(_responsesById, id);

        private static int BinarySearchMaterial(AcousticMaterialProfileDefinition[] values, int id)
        {
            int low = 0;
            int high = values.Length - 1;
            while (low <= high)
            {
                int middle = low + ((high - low) >> 1);
                int candidate = values[middle].Id.Value;
                if (candidate == id)
                    return middle;
                if (candidate < id)
                    low = middle + 1;
                else
                    high = middle - 1;
            }
            return -1;
        }

        private static int BinarySearchMaterialPair(AcousticMaterialPairDefinition[] values, int id)
        {
            int low = 0;
            int high = values.Length - 1;
            while (low <= high)
            {
                int middle = low + ((high - low) >> 1);
                int candidate = values[middle].Id.Value;
                if (candidate == id)
                    return middle;
                if (candidate < id)
                    low = middle + 1;
                else
                    high = middle - 1;
            }
            return -1;
        }

        private static int BinarySearchFamily(PhysicalAudioFamilyDefinition[] values, int id)
        {
            int low = 0;
            int high = values.Length - 1;
            while (low <= high)
            {
                int middle = low + ((high - low) >> 1);
                int candidate = values[middle].Id.Value;
                if (candidate == id)
                    return middle;
                if (candidate < id)
                    low = middle + 1;
                else
                    high = middle - 1;
            }
            return -1;
        }

        private static int BinarySearchBasis(PhysicalAudioBasisDefinition[] values, int id)
        {
            int low = 0;
            int high = values.Length - 1;
            while (low <= high)
            {
                int middle = low + ((high - low) >> 1);
                int candidate = values[middle].Id.Value;
                if (candidate == id)
                    return middle;
                if (candidate < id)
                    low = middle + 1;
                else
                    high = middle - 1;
            }
            return -1;
        }

        private static int BinarySearchResponse(PhysicalAudioResponseProfileDefinition[] values, int id)
        {
            int low = 0;
            int high = values.Length - 1;
            while (low <= high)
            {
                int middle = low + ((high - low) >> 1);
                int candidate = values[middle].Id.Value;
                if (candidate == id)
                    return middle;
                if (candidate < id)
                    low = middle + 1;
                else
                    high = middle - 1;
            }
            return -1;
        }

        private static int GetFamilyKeyIndex(PhysicalAudioInteractionKind kind, PhysicalAudioInteractionMode mode)
        {
            int kindValue = (int)kind;
            int modeValue = (int)mode;
            if ((uint)kindValue >= InteractionKindCount || (uint)modeValue >= InteractionModeCount)
                return -1;
            return (kindValue * InteractionModeCount) + modeValue;
        }

        private static int CompareMaterialIds(AcousticMaterialProfileDefinition left, AcousticMaterialProfileDefinition right) =>
            left.Id.Value.CompareTo(right.Id.Value);

        private static int CompareMaterialPairIds(AcousticMaterialPairDefinition left, AcousticMaterialPairDefinition right) =>
            left.Id.Value.CompareTo(right.Id.Value);

        private static int CompareMaterialPairMaterials(AcousticMaterialPairDefinition left, AcousticMaterialPairDefinition right)
        {
            int materialA = left.MaterialAId.Value.CompareTo(right.MaterialAId.Value);
            if (materialA != 0)
                return materialA;
            int materialB = left.MaterialBId.Value.CompareTo(right.MaterialBId.Value);
            return materialB != 0 ? materialB : left.Id.Value.CompareTo(right.Id.Value);
        }

        private static int CompareFamilyIds(PhysicalAudioFamilyDefinition left, PhysicalAudioFamilyDefinition right) =>
            left.Id.Value.CompareTo(right.Id.Value);

        private static int CompareBasisIds(PhysicalAudioBasisDefinition left, PhysicalAudioBasisDefinition right) =>
            left.Id.Value.CompareTo(right.Id.Value);

        private static int CompareResponseIds(PhysicalAudioResponseProfileDefinition left, PhysicalAudioResponseProfileDefinition right) =>
            left.Id.Value.CompareTo(right.Id.Value);

        private static int CompareFamilyCandidates(PhysicalAudioFamilyDefinition left, PhysicalAudioFamilyDefinition right)
        {
            int kind = left.InteractionKind.CompareTo(right.InteractionKind);
            if (kind != 0)
                return kind;
            int mode = left.InteractionMode.CompareTo(right.InteractionMode);
            if (mode != 0)
                return mode;
            int priority = right.Priority.CompareTo(left.Priority);
            return priority != 0 ? priority : left.Id.Value.CompareTo(right.Id.Value);
        }

        private static int CompareBasisCandidates(PhysicalAudioBasisDefinition left, PhysicalAudioBasisDefinition right)
        {
            int family = left.FamilyId.Value.CompareTo(right.FamilyId.Value);
            if (family != 0)
                return family;
            int priority = right.Priority.CompareTo(left.Priority);
            return priority != 0 ? priority : left.Id.Value.CompareTo(right.Id.Value);
        }

        private static int ComparePairVariantBasisCandidates(PhysicalAudioBasisDefinition left, PhysicalAudioBasisDefinition right)
        {
            int pair = left.MaterialPairId.Value.CompareTo(right.MaterialPairId.Value);
            if (pair != 0)
                return pair;
            int variant = left.VariantKind.CompareTo(right.VariantKind);
            if (variant != 0)
                return variant;
            int priority = right.Priority.CompareTo(left.Priority);
            return priority != 0 ? priority : left.Id.Value.CompareTo(right.Id.Value);
        }

        private readonly struct CandidateRange
        {
            public CandidateRange(int start, int count)
            {
                Start = start;
                Count = count;
            }

            public int Start { get; }
            public int Count { get; }
        }
    }
}
